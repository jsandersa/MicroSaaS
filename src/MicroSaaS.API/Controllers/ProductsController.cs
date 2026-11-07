using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Application.Products.Commands.CreateProduct;
using MicroSaaS.Application.Products.Commands.DeleteProduct;
using MicroSaaS.Application.Products.Commands.UpdateProduct;
using MicroSaaS.Application.Products.Queries.GetProductById;
using MicroSaaS.Application.Products.Queries.GetProducts;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.API.Controllers;

public sealed record BulkInventoryItemDto(
    string Sku,
    string Name,
    int Quantity,
    decimal UnitCost,
    decimal SalePrice);

public sealed record BulkInventoryImportRequest(
    string? DocumentReference,
    bool RegisterAsPurchase,
    int? PurchaseFolio,
    string? SupplierRut,
    string? SupplierName,
    List<BulkInventoryItemDto> Items);

[ApiController]
[Route("api/products")]
[Route("api/inventory")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly TenantDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryNotifier _inventoryNotifier;

    public ProductsController(
        IMediator mediator,
        TenantDbContext context,
        ICurrentUserService currentUserService,
        IInventoryNotifier inventoryNotifier)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _inventoryNotifier = inventoryNotifier ?? throw new ArgumentNullException(nameof(inventoryNotifier));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts(CancellationToken cancellationToken)
    {
        var products = await _mediator.Send(new GetProductsQuery(), cancellationToken);
        return Ok(products);
    }

    [HttpGet("kardex")]
    public async Task<IActionResult> GetKardex(CancellationToken cancellationToken)
    {
        var productsMap = await _context.Products
            .AsNoTracking()
            .ToDictionaryAsync(
                p => p.Id,
                p => new { p.Sku, p.Name, PmpCost = p.PmpCost > 0m ? p.PmpCost : p.CostPrice },
                cancellationToken);

        var transactions = await _context.KardexTransactions
            .AsNoTracking()
            .OrderByDescending(k => k.TransactionDate)
            .Take(200)
            .ToListAsync(cancellationToken);

        var result = transactions.Select(k =>
        {
            productsMap.TryGetValue(k.ProductId, out var prod);
            return new
            {
                Id = k.Id,
                ProductId = k.ProductId,
                ProductSku = prod?.Sku ?? "N/A",
                ProductName = prod?.Name ?? "Producto Eliminado",
                CurrentPmpCost = prod?.PmpCost ?? k.UnitCost,
                MovementType = (int)k.MovementType,
                MovementTypeName = k.MovementType switch
                {
                    MovementType.In => "Entrada (Compra / Ingreso)",
                    MovementType.Out => "Salida (Venta POS)",
                    MovementType.Adjustment => "Ajuste de Inventario",
                    _ => k.MovementType.ToString()
                },
                Quantity = k.Quantity,
                UnitCost = k.UnitCost,
                TotalValue = k.TotalValue,
                TransactionDate = k.TransactionDate,
                Reason = !string.IsNullOrWhiteSpace(k.Reason)
                    ? k.Reason
                    : k.MovementType switch
                    {
                        MovementType.In => "Ingreso por Compra / Stock Inicial",
                        MovementType.Out => "Salida por Venta POS",
                        MovementType.Adjustment => "Ajuste de Inventario",
                        _ => "-"
                    }
            };
        });

        return Ok(result);
    }

    [HttpGet("excel-template")]
    public IActionResult DownloadExcelTemplate()
    {
        var xlsxBytes = BuildBlankInventoryExcelTemplate();
        return File(
            xlsxBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Plantilla_Carga_Inventario_POS.xlsx");
    }

    [HttpPost("parse-excel")]
    public async Task<IActionResult> ParseExcelTemplate(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Debes seleccionar un archivo Excel (.xlsx) válido." });
        }

        try
        {
            var tenantConfig = await _context.Configurations
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            var marginPercentage = tenantConfig is not null && tenantConfig.DefaultMarginPercentage >= 0m
                ? tenantConfig.DefaultMarginPercentage
                : 30.00m;

            var marginFactor = 1m + (marginPercentage / 100m);

            await using var stream = file.OpenReadStream();
            var items = ParseOpenXmlWorkbook(stream, marginFactor);

            if (items.Count == 0)
            {
                return BadRequest(new
                {
                    message = "La planilla Excel está en blanco o aún no contiene filas de productos completadas debajo de los títulos."
                });
            }

            return Ok(items);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = $"No se pudo leer la planilla Excel (.xlsx). Verifica que sea un archivo Excel válido: {ex.Message}"
            });
        }
    }

    private static byte[] BuildBlankInventoryExcelTemplate()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);

            WriteZipEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);

            WriteZipEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Carga_Inventario_POS" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);

            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);

            WriteZipEntry(archive, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <fonts count="4">
                    <font><sz val="11"/><color rgb="FF0F172A"/><name val="Calibri"/></font>
                    <font><b/><sz val="13"/><color rgb="FF1E3A8A"/><name val="Calibri"/></font>
                    <font><sz val="10"/><color rgb="FF334155"/><name val="Calibri"/></font>
                    <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
                  </fonts>
                  <fills count="4">
                    <fill><patternFill patternType="none"/></fill>
                    <fill><patternFill patternType="gray125"/></fill>
                    <fill><patternFill patternType="solid"><fgColor rgb="FFEFF6FF"/><bgColor indexed="64"/></patternFill></fill>
                    <fill><patternFill patternType="solid"><fgColor rgb="FF1D4ED8"/><bgColor indexed="64"/></patternFill></fill>
                  </fills>
                  <borders count="2">
                    <border><left/><right/><top/><bottom/><diagonal/></border>
                    <border>
                      <left style="thin"><color rgb="FFCBD5E1"/></left>
                      <right style="thin"><color rgb="FFCBD5E1"/></right>
                      <top style="thin"><color rgb="FFCBD5E1"/></top>
                      <bottom style="thin"><color rgb="FFCBD5E1"/></bottom>
                      <diagonal/>
                    </border>
                  </borders>
                  <cellStyleXfs count="1">
                    <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
                  </cellStyleXfs>
                  <cellXfs count="5">
                    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                    <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
                    <xf numFmtId="0" fontId="2" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
                    <xf numFmtId="0" fontId="3" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/>
                    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1"/>
                  </cellXfs>
                </styleSheet>
                """);

            var sb = new StringBuilder();
            sb.Append("""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <cols>
                    <col min="1" max="1" width="24" customWidth="1"/>
                    <col min="2" max="2" width="42" customWidth="1"/>
                    <col min="3" max="3" width="22" customWidth="1"/>
                    <col min="4" max="4" width="28" customWidth="1"/>
                    <col min="5" max="5" width="30" customWidth="1"/>
                  </cols>
                  <sheetData>
                    <row r="1" ht="24" customHeight="1">
                      <c r="A1" s="1" t="inlineStr"><is><t>PLANTILLA OFICIAL DE CARGA DE INVENTARIO INICIAL — MICROSAAS POS</t></is></c>
                      <c r="B1" s="1"/><c r="C1" s="1"/><c r="D1" s="1"/><c r="E1" s="1"/>
                    </row>
                    <row r="2" ht="19" customHeight="1">
                      <c r="A2" s="2" t="inlineStr"><is><t>INSTRUCCIONES: Escriba sus productos a partir de la Fila 6 (debajo de los titulos azules). No modifique la Fila 5.</t></is></c>
                      <c r="B2" s="2"/><c r="C2" s="2"/><c r="D2" s="2"/><c r="E2" s="2"/>
                    </row>
                    <row r="3" ht="19" customHeight="1">
                      <c r="A3" s="2" t="inlineStr"><is><t>• SKU: Codigo unico (Ej: PROD-01) | • CANTIDAD: Stock a ingresar | • COSTO NETO: Costo sin IVA | • PRECIO VENTA: Si lo deja vacio se calcula automatico.</t></is></c>
                      <c r="B3" s="2"/><c r="C3" s="2"/><c r="D3" s="2"/><c r="E3" s="2"/>
                    </row>
                    <row r="5" ht="24" customHeight="1">
                      <c r="A5" s="3" t="inlineStr"><is><t>CODIGO SKU *</t></is></c>
                      <c r="B5" s="3" t="inlineStr"><is><t>NOMBRE DEL PRODUCTO *</t></is></c>
                      <c r="C5" s="3" t="inlineStr"><is><t>CANTIDAD INICIAL *</t></is></c>
                      <c r="D5" s="3" t="inlineStr"><is><t>COSTO UNITARIO NETO (CLP) *</t></is></c>
                      <c r="E5" s="3" t="inlineStr"><is><t>PRECIO VENTA POS (OPCIONAL)</t></is></c>
                    </row>
                """);

            for (int r = 6; r <= 55; r++)
            {
                sb.Append($"""
                    <row r="{r}" ht="20" customHeight="1">
                      <c r="A{r}" s="4"/>
                      <c r="B{r}" s="4"/>
                      <c r="C{r}" s="4"/>
                      <c r="D{r}" s="4"/>
                      <c r="E{r}" s="4"/>
                    </row>
                """);
            }

            sb.Append("""
                  </sheetData>
                </worksheet>
                """);

            WriteZipEntry(archive, "xl/worksheets/sheet1.xml", sb.ToString());
        }

        return ms.ToArray();
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }

    private static List<BulkInventoryItemDto> ParseOpenXmlWorkbook(Stream stream, decimal marginFactor)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        var sharedStrings = new List<string>();
        var sharedStringsEntry = archive.Entries.FirstOrDefault(
            e => e.FullName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));

        if (sharedStringsEntry is not null)
        {
            using var ssStream = sharedStringsEntry.Open();
            var ssDoc = XDocument.Load(ssStream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var si in ssDoc.Descendants(ns + "si"))
            {
                var text = string.Concat(si.Descendants(ns + "t").Select(t => t.Value));
                sharedStrings.Add(text);
            }
        }

        var sheetEntry = archive.Entries.FirstOrDefault(
            e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                 e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

        if (sheetEntry is null)
        {
            throw new InvalidOperationException("No se encontró ninguna hoja de cálculo en el archivo Excel.");
        }

        using var sheetStream = sheetEntry.Open();
        var sheetDoc = XDocument.Load(sheetStream);
        XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var items = new List<BulkInventoryItemDto>();

        foreach (var row in sheetDoc.Descendants(mainNs + "row"))
        {
            var rowNumberAttr = row.Attribute("r")?.Value;
            if (int.TryParse(rowNumberAttr, out var rowNumber) && rowNumber <= 4)
            {
                continue;
            }

            var cols = new string[5];
            foreach (var cell in row.Elements(mainNs + "c"))
            {
                var cellRef = cell.Attribute("r")?.Value ?? string.Empty;
                var colIndex = GetColumnIndexFromCellReference(cellRef);
                if (colIndex < 0 || colIndex >= cols.Length)
                {
                    continue;
                }

                cols[colIndex] = ReadCellValue(cell, mainNs, sharedStrings).Trim();
            }

            var rawSku = cols[0] ?? string.Empty;
            var rawName = cols[1] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawSku) && string.IsNullOrWhiteSpace(rawName))
            {
                continue;
            }

            // Ignorar fila de encabezados o filas de instrucciones
            var upperCol0 = rawSku.ToUpperInvariant();
            if (upperCol0.Contains("CODIGO SKU") ||
                upperCol0.Contains("PLANTILLA OFICIAL") ||
                upperCol0.Contains("INSTRUCCIONES") ||
                upperCol0.StartsWith("•"))
            {
                continue;
            }

            var sku = (!string.IsNullOrWhiteSpace(rawSku) ? rawSku : rawName).Trim().ToUpperInvariant();
            var name = (!string.IsNullOrWhiteSpace(rawName) ? rawName : sku).Trim();
            var quantity = Math.Max(0, (int)Math.Round(ParseDecimalSafe(cols[2]), 0, MidpointRounding.AwayFromZero));
            var unitCost = Math.Max(0m, Math.Round(ParseDecimalSafe(cols[3]), 0, MidpointRounding.AwayFromZero));
            var rawSalePrice = Math.Round(ParseDecimalSafe(cols[4]), 0, MidpointRounding.AwayFromZero);
            var salePrice = rawSalePrice > 0m
                ? rawSalePrice
                : Math.Max(1m, Math.Round(unitCost * marginFactor, 0, MidpointRounding.AwayFromZero));

            items.Add(new BulkInventoryItemDto(
                Sku: sku,
                Name: name,
                Quantity: quantity,
                UnitCost: unitCost,
                SalePrice: salePrice));
        }

        return items;
    }

    private static int GetColumnIndexFromCellReference(string cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return -1;
        }

        int colIndex = 0;
        foreach (char ch in cellReference)
        {
            if (char.IsLetter(ch))
            {
                colIndex = (colIndex * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
            }
            else
            {
                break;
            }
        }

        return colIndex - 1;
    }

    private static string ReadCellValue(XElement cell, XNamespace ns, List<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
        }

        var v = cell.Element(ns + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(v, out var ssIndex) && ssIndex >= 0 && ssIndex < sharedStrings.Count)
        {
            return sharedStrings[ssIndex];
        }

        return v;
    }

    private static decimal ParseDecimalSafe(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0m;
        }

        var cleaned = raw.Replace("$", string.Empty).Replace(" ", string.Empty).Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }

        if (decimal.TryParse(cleaned, NumberStyles.Any, new CultureInfo("es-CL"), out val))
        {
            return val;
        }

        return 0m;
    }

    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImport(
        [FromBody] BulkInventoryImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "La carga masiva debe incluir al menos un producto válido." });
        }

        var tenantGuid = _context.CurrentTenantId ?? _currentUserService.TenantId;
        if (tenantGuid == Guid.Empty)
        {
            return Unauthorized(new { message = "No se pudo identificar la sesión empresarial (TenantId)." });
        }

        var tenantIdStr = tenantGuid.ToString();
        var docReference = !string.IsNullOrWhiteSpace(request.DocumentReference)
            ? request.DocumentReference.Trim()
            : $"Acta Carga Inicial {DateTime.UtcNow:dd/MM/yyyy}";

        var stockNotifications = new List<(Guid ProductId, int NewStock)>();

        try
        {
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            var summary = await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                var tenantConfig = await _context.Configurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken);

                var marginPercentage = tenantConfig is not null && tenantConfig.DefaultMarginPercentage >= 0m
                    ? tenantConfig.DefaultMarginPercentage
                    : 30.00m;

                var existingProducts = await _context.Products.ToListAsync(cancellationToken);
                var productsBySku = existingProducts
                    .GroupBy(p => p.Sku.Trim().ToUpperInvariant())
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                int createdCount = 0;
                int updatedCount = 0;
                int totalUnitsAdded = 0;
                decimal totalNetCost = 0m;

                var purchaseItems = new List<PurchaseItem>();

                foreach (var rawItem in request.Items)
                {
                    if (string.IsNullOrWhiteSpace(rawItem.Sku) && string.IsNullOrWhiteSpace(rawItem.Name))
                    {
                        continue;
                    }

                    var sku = (!string.IsNullOrWhiteSpace(rawItem.Sku) ? rawItem.Sku : rawItem.Name)
                        .Trim()
                        .ToUpperInvariant();
                    if (sku.Length > 50)
                    {
                        sku = sku[..50];
                    }

                    var name = (!string.IsNullOrWhiteSpace(rawItem.Name) ? rawItem.Name : sku).Trim();
                    if (name.Length > 150)
                    {
                        name = name[..150];
                    }

                    var qty = Math.Max(0, rawItem.Quantity);
                    var unitCost = Math.Max(0m, rawItem.UnitCost);
                    var suggestedSalePrice = unitCost > 0m
                        ? Math.Round(unitCost * (1m + (marginPercentage / 100m)), 0, MidpointRounding.AwayFromZero)
                        : 1000m;

                    var salePrice = rawItem.SalePrice > 0m ? rawItem.SalePrice : Math.Max(1m, suggestedSalePrice);

                    if (productsBySku.TryGetValue(sku, out var existingProduct))
                    {
                        if (qty > 0)
                        {
                            existingProduct.RegisterStockIn(qty, unitCost, marginPercentage);
                            if (rawItem.SalePrice > 0m)
                            {
                                existingProduct.Update(
                                    existingProduct.Sku,
                                    name,
                                    rawItem.SalePrice,
                                    existingProduct.PmpCost,
                                    existingProduct.CurrentStock);
                            }

                            var kardexIn = KardexTransaction.Create(
                                tenantId: tenantGuid,
                                productId: existingProduct.Id,
                                movementType: MovementType.In,
                                quantity: qty,
                                unitCost: unitCost,
                                reason: $"[+{qty} u.] Carga Masiva ({docReference}) — PMP resultante: ${Math.Round(existingProduct.PmpCost, 0):N0}");

                            await _context.KardexTransactions.AddAsync(kardexIn, cancellationToken);
                            totalUnitsAdded += qty;
                            totalNetCost += qty * unitCost;
                        }
                        else
                        {
                            existingProduct.Update(
                                existingProduct.Sku,
                                name,
                                salePrice,
                                unitCost > 0m ? unitCost : existingProduct.PmpCost,
                                existingProduct.CurrentStock);
                        }

                        updatedCount++;
                        stockNotifications.Add((existingProduct.Id, existingProduct.CurrentStock));
                    }
                    else
                    {
                        var newProduct = new Product(
                            tenantId: tenantIdStr,
                            sku: sku,
                            name: name,
                            salePrice: salePrice,
                            costPrice: unitCost,
                            currentStock: qty);

                        await _context.Products.AddAsync(newProduct, cancellationToken);
                        productsBySku[sku] = newProduct;

                        if (qty > 0)
                        {
                            var initialKardex = KardexTransaction.Create(
                                tenantId: tenantGuid,
                                productId: newProduct.Id,
                                movementType: MovementType.In,
                                quantity: qty,
                                unitCost: unitCost,
                                reason: $"[+{qty} u.] Carga Masiva Inicial ({docReference})");

                            await _context.KardexTransactions.AddAsync(initialKardex, cancellationToken);
                            totalUnitsAdded += qty;
                            totalNetCost += qty * unitCost;
                        }

                        createdCount++;
                        stockNotifications.Add((newProduct.Id, newProduct.CurrentStock));
                    }

                    if (request.RegisterAsPurchase && qty > 0)
                    {
                        purchaseItems.Add(new PurchaseItem
                        {
                            Id = Guid.NewGuid(),
                            NombreItem = name,
                            Cantidad = qty,
                            PrecioUnitario = unitCost,
                            MontoItem = Math.Round(qty * unitCost, 0, MidpointRounding.AwayFromZero)
                        });
                    }
                }

                if (request.RegisterAsPurchase && purchaseItems.Count > 0)
                {
                    var net = Math.Round(totalNetCost, 0, MidpointRounding.AwayFromZero);
                    var iva = Math.Round(net * 0.19m, 0, MidpointRounding.AwayFromZero);
                    var folio = request.PurchaseFolio.HasValue && request.PurchaseFolio.Value > 0
                        ? request.PurchaseFolio.Value
                        : int.Parse(DateTime.UtcNow.ToString("HHmmss"));

                    var purchase = new Purchase
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantGuid,
                        Folio = folio,
                        TipoDte = 33,
                        FechaEmision = DateTime.UtcNow,
                        RutProveedor = !string.IsNullOrWhiteSpace(request.SupplierRut)
                            ? request.SupplierRut.Trim()
                            : "76.000.000-0",
                        RazonSocialProveedor = !string.IsNullOrWhiteSpace(request.SupplierName)
                            ? request.SupplierName.Trim()
                            : docReference,
                        MontoNeto = net,
                        Iva = iva,
                        MontoTotal = net + iva
                    };

                    foreach (var pItem in purchaseItems)
                    {
                        pItem.PurchaseId = purchase.Id;
                        purchase.Items.Add(pItem);
                    }

                    await _context.Purchases.AddAsync(purchase, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new
                {
                    CreatedCount = createdCount,
                    UpdatedCount = updatedCount,
                    TotalUnitsAdded = totalUnitsAdded,
                    TotalNetCost = Math.Round(totalNetCost, 0, MidpointRounding.AwayFromZero),
                    DocumentReference = docReference,
                    RegisteredAsPurchase = request.RegisterAsPurchase && purchaseItems.Count > 0,
                    Message = $"Carga masiva completada ({docReference}): {createdCount} productos creados, {updatedCount} actualizados y +{totalUnitsAdded} unidades ingresadas al Kardex."
                };
            });

            foreach (var (productId, newStock) in stockNotifications)
            {
                await _inventoryNotifier.NotifyStockUpdateAsync(productId, newStock, cancellationToken);
            }

            return Ok(summary);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Error al procesar la carga masiva: {ex.Message}" });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var productId = await _mediator.Send(command, cancellationToken);
        return Ok(new { Id = productId });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var effectiveCommand = command with { Id = id };
            var updated = await _mediator.Send(effectiveCommand, cancellationToken);

            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteProductCommand(id), cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
