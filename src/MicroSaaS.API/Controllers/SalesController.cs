using System.IO.Compression;
using System.Security;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Application.Sales.Commands.RegisterSale;
using MicroSaaS.Application.Sales.Queries.GetSales;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.API.Controllers;

public record EmitCreditNoteRequest(string? Reason = null);

[ApiController]
[Route("api/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IDteRepository _dteRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly TenantDbContext _dbContext;
    private readonly IInventoryNotifier _inventoryNotifier;

    public SalesController(
        IMediator mediator,
        IDteRepository dteRepository,
        ICurrentUserService currentUserService,
        TenantDbContext dbContext,
        IInventoryNotifier inventoryNotifier)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _dteRepository = dteRepository ?? throw new ArgumentNullException(nameof(dteRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _inventoryNotifier = inventoryNotifier ?? throw new ArgumentNullException(nameof(inventoryNotifier));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SaleSummaryDto>>> GetSales(CancellationToken cancellationToken)
    {
        var sales = await _mediator.Send(new GetSalesQuery(), cancellationToken);
        return Ok(sales);
    }

    [HttpGet("dashboard-summary")]
    public async Task<IActionResult> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        var nowUtc = DateTime.UtcNow;
        var startOfTodayUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        var startOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var products = await _dbContext.Products
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var productsMap = products.ToDictionary(
            p => p.Id,
            p => new
            {
                p.Sku,
                p.Name,
                Pmp = p.PmpCost > 0m ? p.PmpCost : p.CostPrice,
                p.SalePrice,
                p.CurrentStock
            });

        var dtes = await _dbContext.DteDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var annulledSaleIds = dtes
            .Where(d => (d.DteType == DteType.BoletaElectronica || d.DteType == DteType.FacturaElectronica)
                        && d.Status == DteStatus.Rejected)
            .Select(d => d.SaleId)
            .ToHashSet();

        var creditNotesCount = dtes.Count(d => d.DteType == DteType.NotaCredito);
        var creditNotesTotal = dtes
            .Where(d => d.DteType == DteType.NotaCredito)
            .Sum(d => d.TotalAmount);

        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .ToListAsync(cancellationToken);

        var activeSales = sales
            .Where(s => !annulledSaleIds.Contains(s.Id))
            .ToList();

        var todaySales = activeSales
            .Where(s => s.FechaEmision >= startOfTodayUtc)
            .ToList();

        var monthSales = activeSales
            .Where(s => s.FechaEmision >= startOfMonthUtc)
            .ToList();

        var monthPurchases = await _dbContext.Purchases
            .AsNoTracking()
            .Where(p => p.FechaEmision >= startOfMonthUtc)
            .ToListAsync(cancellationToken);

        var todaySalesTotal = todaySales.Sum(s => s.MontoTotal > 0m ? s.MontoTotal : s.TotalAmount);
        var todaySalesCount = todaySales.Count;

        var monthSalesTotal = monthSales.Sum(s => s.MontoTotal > 0m ? s.MontoTotal : s.TotalAmount);
        var monthSalesNet = monthSales.Sum(s => s.MontoNeto > 0m ? s.MontoNeto : Math.Round(s.TotalAmount / 1.19m, 0));
        var monthIvaDebito = monthSales.Sum(s => s.Iva > 0m ? s.Iva : (s.TotalAmount - Math.Round(s.TotalAmount / 1.19m, 0)));
        var monthSalesCount = monthSales.Count;

        var monthPurchasesTotal = monthPurchases.Sum(p => p.MontoTotal);
        var monthPurchasesNet = monthPurchases.Sum(p => p.MontoNeto);
        var monthIvaCredito = monthPurchases.Sum(p => p.Iva);
        var monthPurchasesCount = monthPurchases.Count;

        var ivaAPagar = Math.Max(0m, monthIvaDebito - monthIvaCredito);
        var remanenteCreditoFiscal = Math.Max(0m, monthIvaCredito - monthIvaDebito);

        // Costo de Ventas (COGS) valorizado a PMP y Utilidad Bruta Real
        decimal monthCogsPmp = 0m;
        var productSalesAgg = new Dictionary<Guid, (string Sku, string Name, int Units, decimal Revenue, decimal CostPmp)>();

        foreach (var sale in monthSales)
        {
            foreach (var item in sale.Items)
            {
                var qty = item.Quantity > 0 ? item.Quantity : (int)Math.Round(item.Cantidad, 0);
                var unitPrice = item.UnitPrice > 0m ? item.UnitPrice : item.PrecioUnitario;
                var lineRevenue = qty * unitPrice;

                productsMap.TryGetValue(item.ProductId, out var prodInfo);
                var unitPmp = prodInfo?.Pmp ?? Math.Round(unitPrice * 0.7m, 0);
                var lineCost = qty * unitPmp;

                monthCogsPmp += lineCost;

                var key = item.ProductId != Guid.Empty ? item.ProductId : Guid.Empty;
                var sku = prodInfo?.Sku ?? "SKU";
                var name = prodInfo?.Name ?? (!string.IsNullOrWhiteSpace(item.NombreItem) ? item.NombreItem : "Producto");

                if (productSalesAgg.TryGetValue(key, out var existing))
                {
                    productSalesAgg[key] = (
                        existing.Sku,
                        existing.Name,
                        existing.Units + qty,
                        existing.Revenue + lineRevenue,
                        existing.CostPmp + lineCost);
                }
                else
                {
                    productSalesAgg[key] = (sku, name, qty, lineRevenue, lineCost);
                }
            }
        }

        var monthGrossProfit = monthSalesNet - monthCogsPmp;

        var topProducts = productSalesAgg.Values
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .Select(x => new
            {
                Sku = x.Sku,
                Name = x.Name,
                UnitsSold = x.Units,
                Revenue = x.Revenue,
                CostPmp = x.CostPmp,
                GrossProfit = x.Revenue - x.CostPmp
            })
            .ToList();

        var inventoryValorizedPmp = products.Sum(p => p.CurrentStock * (p.PmpCost > 0m ? p.PmpCost : p.CostPrice));
        var inventoryExpectedSaleValue = products.Sum(p => p.CurrentStock * p.SalePrice);
        var criticalStockProducts = products
            .Where(p => p.CurrentStock <= 5)
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Name,
                p.CurrentStock,
                PmpCost = p.PmpCost > 0m ? p.PmpCost : p.CostPrice
            })
            .ToList();

        var cafList = await _dbContext.CafAuthorizations
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DteType)
            .ToListAsync(cancellationToken);

        var maxFoliosByType = dtes
            .GroupBy(d => (int)d.DteType)
            .ToDictionary(g => g.Key, g => g.Max(d => d.Folio));

        var cafSummary = cafList.Select(caf =>
        {
            maxFoliosByType.TryGetValue(caf.DteType, out var maxFolio);
            var effectiveCurrent = Math.Max(caf.CurrentFolio, maxFolio);
            var total = Math.Max(1, caf.EndFolio - caf.StartFolio + 1);
            var used = effectiveCurrent >= caf.StartFolio ? Math.Min(total, effectiveCurrent - caf.StartFolio + 1) : 0;
            var available = Math.Max(0, total - used);

            return new
            {
                DteType = caf.DteType,
                DteTypeName = caf.DteType switch
                {
                    33 => "Factura Electrónica (33)",
                    39 => "Boleta Electrónica (39)",
                    61 => "Nota de Crédito (61)",
                    _ => $"DTE {caf.DteType}"
                },
                StartFolio = caf.StartFolio,
                EndFolio = caf.EndFolio,
                CurrentFolio = effectiveCurrent,
                AvailableFolios = available,
                UsagePercentage = Math.Round((decimal)used / total * 100m, 1)
            };
        });

        return Ok(new
        {
            TodaySalesTotal = todaySalesTotal,
            TodaySalesCount = todaySalesCount,
            MonthSalesTotal = monthSalesTotal,
            MonthSalesNet = monthSalesNet,
            MonthIvaDebito = monthIvaDebito,
            MonthSalesCount = monthSalesCount,
            MonthPurchasesTotal = monthPurchasesTotal,
            MonthPurchasesNet = monthPurchasesNet,
            MonthIvaCredito = monthIvaCredito,
            MonthPurchasesCount = monthPurchasesCount,
            IvaAPagar = ivaAPagar,
            RemanenteCreditoFiscal = remanenteCreditoFiscal,
            MonthCogsPmp = monthCogsPmp,
            MonthGrossProfit = monthGrossProfit,
            CreditNotesCount = creditNotesCount,
            CreditNotesTotal = creditNotesTotal,
            InventoryValorizedPmp = inventoryValorizedPmp,
            InventoryExpectedSaleValue = inventoryExpectedSaleValue,
            TotalProductsCount = products.Count,
            CriticalStockProducts = criticalStockProducts,
            TopProducts = topProducts,
            CafSummary = cafSummary
        });
    }

    [HttpGet("accounting-export-excel")]
    public async Task<IActionResult> DownloadAccountingExcelPack(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        var nowUtc = DateTime.UtcNow;
        var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .OrderByDescending(s => s.FechaEmision)
            .ToListAsync(cancellationToken);

        var dtes = await _dbContext.DteDocuments
            .AsNoTracking()
            .OrderByDescending(d => d.EmissionDate)
            .ToListAsync(cancellationToken);

        var purchases = await _dbContext.Purchases
            .AsNoTracking()
            .OrderByDescending(p => p.FechaEmision)
            .ToListAsync(cancellationToken);

        var products = await _dbContext.Products
            .AsNoTracking()
            .OrderBy(p => p.Sku)
            .ToListAsync(cancellationToken);

        var kardex = await _dbContext.KardexTransactions
            .AsNoTracking()
            .OrderByDescending(k => k.TransactionDate)
            .Take(300)
            .ToListAsync(cancellationToken);

        var annulledSaleIds = dtes
            .Where(d => d.Status == DteStatus.Rejected && d.SaleId != Guid.Empty)
            .Select(d => d.SaleId)
            .ToHashSet();

        var monthActiveSales = sales
            .Where(s => s.FechaEmision >= monthStartUtc && !annulledSaleIds.Contains(s.Id))
            .ToList();

        var monthPurchases = purchases
            .Where(p => p.FechaEmision >= monthStartUtc)
            .ToList();

        var creditNotes = dtes
            .Where(d => (int)d.DteType == 61 && d.EmissionDate >= monthStartUtc)
            .ToList();

        var monthSalesTotal = monthActiveSales.Sum(s => s.MontoTotal);
        var monthSalesNet = monthActiveSales.Sum(s => s.MontoNeto);
        var monthIvaDebito = monthActiveSales.Sum(s => s.Iva);

        var monthPurchasesTotal = monthPurchases.Sum(p => p.MontoTotal);
        var monthPurchasesNet = monthPurchases.Sum(p => p.MontoNeto);
        var monthIvaCredito = monthPurchases.Sum(p => p.Iva);

        var ivaBalance = monthIvaDebito - monthIvaCredito;
        var ivaAPagar = ivaBalance > 0m ? ivaBalance : 0m;
        var remanente = ivaBalance < 0m ? Math.Abs(ivaBalance) : 0m;

        var productsById = products.ToDictionary(p => p.Id, p => p);
        decimal monthCogsPmp = 0m;
        foreach (var sale in monthActiveSales)
        {
            foreach (var item in sale.Items)
            {
                var qty = item.Quantity > 0 ? item.Quantity : (int)Math.Round(item.Cantidad, 0);
                if (item.ProductId != Guid.Empty && productsById.TryGetValue(item.ProductId, out var prod))
                {
                    var pmp = prod.PmpCost > 0m ? prod.PmpCost : prod.CostPrice;
                    monthCogsPmp += qty * pmp;
                }
            }
        }
        monthCogsPmp = Math.Round(monthCogsPmp, 0, MidpointRounding.AwayFromZero);
        var monthGrossProfit = Math.Round(monthSalesNet - monthCogsPmp, 0, MidpointRounding.AwayFromZero);
        var inventoryValorizedPmp = Math.Round(
            products.Sum(p => p.CurrentStock * (p.PmpCost > 0m ? p.PmpCost : p.CostPrice)),
            0,
            MidpointRounding.AwayFromZero);

        var salesById = sales.ToDictionary(s => s.Id, s => s);

        // Construir las 4 hojas del libro Excel Contable
        var sheet1Rows = new List<string[]>
        {
            new[] { "CONCEPTO CONTABLE / TRIBUTARIO (FORMULARIO 29 SII)", "CANTIDAD DOCS", "MONTO (CLP)", "OBSERVACIÓN AUDITORÍA" },
            new[] { "Ventas Brutas Vigentes del Período (IVA Incluido)", monthActiveSales.Count.ToString(), Math.Round(monthSalesTotal, 0).ToString("0"), "Excluye ventas anuladas por Nota de Crédito (61)" },
            new[] { "Ventas Netas Afectas del Período", monthActiveSales.Count.ToString(), Math.Round(monthSalesNet, 0).ToString("0"), "Base imponible afecta a IVA" },
            new[] { "(+) IVA Débito Fiscal (19% Ventas Vigentes)", monthActiveSales.Count.ToString(), Math.Round(monthIvaDebito, 0).ToString("0"), "Impuesto determinado sobre ventas vigentes" },
            new[] { "Notas de Crédito Electrónicas (DTE 61) Emitidas", creditNotes.Count.ToString(), Math.Round(creditNotes.Sum(c => c.TotalAmount), 0).ToString("0"), "Devoluciones y anulaciones con reingreso a Kardex" },
            new[] { "Compras Netas con Factura Recibidas (DTE 33)", monthPurchases.Count.ToString(), Math.Round(monthPurchasesNet, 0).ToString("0"), "Respaldo de ingreso de mercadería" },
            new[] { "(-) IVA Crédito Fiscal Recuperable (19% Compras)", monthPurchases.Count.ToString(), Math.Round(monthIvaCredito, 0).ToString("0"), "Crédito fiscal por compras del giro" },
            new[] { "(=) IMPUESTO IVA DETERMINADO A PAGAR (F29)", "-", Math.Round(ivaAPagar, 0).ToString("0"), ivaAPagar > 0 ? "Saldo a enterar en arcas fiscales" : "Sin pago de IVA en el período" },
            new[] { "(=) REMANENTE DE CRÉDITO FISCAL MES SIGUIENTE", "-", Math.Round(remanente, 0).ToString("0"), remanente > 0 ? "Saldo a favor acumulable para período siguiente" : "Sin remanente" },
            new[] { "Costo de Ventas Real sobre PMP (COGS)", "-", monthCogsPmp.ToString("0"), "Valorizado según Precio Medio Ponderado (Art. 30 LIR)" },
            new[] { "(=) UTILIDAD BRUTA CONTABLE REAL DEL PERÍODO", "-", monthGrossProfit.ToString("0"), "Venta Neta menos Costo de Ventas PMP" },
            new[] { "Valorización Final de Existencias en Bodega (PMP)", products.Count.ToString(), inventoryValorizedPmp.ToString("0"), "Saldo de Activo Realizable para Balance General" }
        };

        var sheet2Rows = new List<string[]>
        {
            new[] { "FECHA EMISIÓN", "CÓDIGO DTE", "TIPO DOCUMENTO SII", "FOLIO SII", "RUT RECEPTOR", "RAZÓN SOCIAL RECEPTOR", "MONTO NETO (CLP)", "IVA 19% (CLP)", "MONTO TOTAL (CLP)", "ESTADO TRIBUTARIO" }
        };

        foreach (var d in dtes)
        {
            salesById.TryGetValue(d.SaleId, out var linkedSale);
            var total = Math.Round(d.TotalAmount, 0, MidpointRounding.AwayFromZero);
            var net = linkedSale is not null ? Math.Round(linkedSale.MontoNeto, 0) : Math.Round(total / 1.19m, 0);
            var iva = linkedSale is not null ? Math.Round(linkedSale.Iva, 0) : (total - net);
            var rut = linkedSale?.RutCliente ?? "66.666.666-6";
            var rzn = linkedSale?.RazonSocialCliente ?? ((int)d.DteType == 61 ? "Receptor Nota de Crédito" : "Consumidor Final POS");
            var dteName = (int)d.DteType switch
            {
                33 => "Factura Electrónica (33)",
                39 => "Boleta Electrónica (39)",
                61 => "Nota de Crédito Electrónica (61)",
                _ => $"DTE {(int)d.DteType}"
            };
            var statusName = d.Status == DteStatus.Rejected ? "ANULADO POR NOTA DE CRÉDITO (61)" : "VIGENTE / EMITIDO SII";

            sheet2Rows.Add(new[]
            {
                d.EmissionDate.ToString("dd/MM/yyyy HH:mm"),
                ((int)d.DteType).ToString(),
                dteName,
                d.Folio.ToString(),
                rut,
                rzn,
                net.ToString("0"),
                iva.ToString("0"),
                total.ToString("0"),
                statusName
            });
        }

        var sheet3Rows = new List<string[]>
        {
            new[] { "FECHA EMISIÓN", "CÓDIGO DTE", "FOLIO FACTURA", "RUT PROVEEDOR", "RAZÓN SOCIAL PROVEEDOR", "MONTO NETO (CLP)", "IVA CRÉDITO FISCAL (CLP)", "MONTO TOTAL (CLP)" }
        };

        foreach (var p in purchases)
        {
            sheet3Rows.Add(new[]
            {
                p.FechaEmision.ToString("dd/MM/yyyy"),
                p.TipoDte.ToString(),
                p.Folio.ToString(),
                p.RutProveedor,
                p.RazonSocialProveedor,
                Math.Round(p.MontoNeto, 0).ToString("0"),
                Math.Round(p.Iva, 0).ToString("0"),
                Math.Round(p.MontoTotal, 0).ToString("0")
            });
        }

        var sheet4Rows = new List<string[]>
        {
            new[] { "SECCIÓN", "SKU / FECHA", "PRODUCTO", "CANTIDAD / STOCK", "COSTO UNITARIO PMP (CLP)", "VALOR TOTAL PMP (CLP)", "PRECIO VENTA POS / GLOSA AUDITORÍA" }
        };

        foreach (var prod in products)
        {
            var pmp = Math.Round(prod.PmpCost > 0m ? prod.PmpCost : prod.CostPrice, 0);
            var totalVal = prod.CurrentStock * pmp;
            sheet4Rows.Add(new[]
            {
                "EXISTENCIA ACTUAL",
                prod.Sku,
                prod.Name,
                prod.CurrentStock.ToString(),
                pmp.ToString("0"),
                totalVal.ToString("0"),
                $"Precio Venta POS: ${Math.Round(prod.SalePrice, 0):N0}"
            });
        }

        foreach (var k in kardex)
        {
            productsById.TryGetValue(k.ProductId, out var prod);
            var movName = k.MovementType switch
            {
                MovementType.In => "KARDEX ENTRADA (+)",
                MovementType.Out => "KARDEX SALIDA (-)",
                _ => "KARDEX AJUSTE (=)"
            };
            sheet4Rows.Add(new[]
            {
                movName,
                k.TransactionDate.ToString("dd/MM/yyyy HH:mm"),
                prod is not null ? $"{prod.Sku} - {prod.Name}" : "Producto",
                k.Quantity.ToString(),
                Math.Round(k.UnitCost, 0).ToString("0"),
                Math.Round(k.TotalValue, 0).ToString("0"),
                k.Reason ?? "-"
            });
        }

        var excelBytes = BuildMultiSheetAccountingWorkbook(
            ("1_PreBalance_F29_SII", "PRE-BALANCE MENSUAL IVA FORMULARIO 29 Y ESTADO DE RESULTADOS PMP", sheet1Rows),
            ("2_Libro_Ventas_DTE", "LIBRO DE VENTAS Y DOCUMENTOS TRIBUTARIOS ELECTRÓNICOS EMITIDOS (DTE 33, 39, 61)", sheet2Rows),
            ("3_Libro_Compras", "LIBRO DE COMPRAS Y RESPALDO DE IVA CRÉDITO FISCAL", sheet3Rows),
            ("4_Inventario_Kardex_PMP", "EXISTENCIAS VALORIZADAS AL COSTO MEDIO PONDERADO (PMP) Y LIBRO MAYOR KARDEX", sheet4Rows));

        return File(
            excelBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Pack_Contable_SII_F29_{nowUtc:yyyy_MM}.xlsx");
    }

    private static byte[] BuildMultiSheetAccountingWorkbook(
        params (string SheetName, string TitleBanner, List<string[]> Rows)[] sheets)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var overridesSb = new StringBuilder();
            var sheetsXmlSb = new StringBuilder();
            var relsXmlSb = new StringBuilder();

            for (int i = 0; i < sheets.Length; i++)
            {
                int idx = i + 1;
                overridesSb.AppendLine(
                    $"""  <Override PartName="/xl/worksheets/sheet{idx}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
                sheetsXmlSb.AppendLine(
                    $"""    <sheet name="{SecurityElement.Escape(sheets[i].SheetName)}" sheetId="{idx}" r:id="rId{idx}"/>""");
                relsXmlSb.AppendLine(
                    $"""  <Relationship Id="rId{idx}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{idx}.xml"/>""");
            }

            int stylesRelId = sheets.Length + 1;
            relsXmlSb.AppendLine(
                $"""  <Relationship Id="rId{stylesRelId}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");

            WriteZipText(archive, "[Content_Types].xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                {overridesSb}</Types>
                """);

            WriteZipText(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);

            WriteZipText(archive, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                {sheetsXmlSb}  </sheets>
                </workbook>
                """);

            WriteZipText(archive, "xl/_rels/workbook.xml.rels", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                {relsXmlSb}</Relationships>
                """);

            WriteZipText(archive, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <fonts count="3">
                    <font><sz val="11"/><color rgb="FF0F172A"/><name val="Calibri"/></font>
                    <font><b/><sz val="12"/><color rgb="FF1E3A8A"/><name val="Calibri"/></font>
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
                  <cellXfs count="4">
                    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                    <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
                    <xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/>
                    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1"/>
                  </cellXfs>
                </styleSheet>
                """);

            for (int s = 0; s < sheets.Length; s++)
            {
                var sheet = sheets[s];
                var sb = new StringBuilder();
                sb.Append("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                      <cols>
                        <col min="1" max="1" width="38" customWidth="1"/>
                        <col min="2" max="10" width="24" customWidth="1"/>
                      </cols>
                      <sheetData>
                    """);

                sb.Append($"""
                        <row r="1" ht="24" customHeight="1">
                          <c r="A1" s="1" t="inlineStr"><is><t>{SecurityElement.Escape(sheet.TitleBanner)}</t></is></c>
                        </row>
                    """);

                for (int rIdx = 0; rIdx < sheet.Rows.Count; rIdx++)
                {
                    int excelRow = rIdx + 3;
                    int styleId = rIdx == 0 ? 2 : 3;
                    var cells = sheet.Rows[rIdx];
                    sb.Append($"""    <row r="{excelRow}" ht="20" customHeight="1">""");

                    for (int cIdx = 0; cIdx < cells.Length; cIdx++)
                    {
                        string colLetter = ((char)('A' + cIdx)).ToString();
                        string cellVal = cells[cIdx] ?? string.Empty;
                        if (rIdx > 0 && decimal.TryParse(cellVal, out var numVal) && cIdx >= 1)
                        {
                            sb.Append($"""<c r="{colLetter}{excelRow}" s="{styleId}"><v>{numVal}</v></c>""");
                        }
                        else
                        {
                            sb.Append(
                                $"""<c r="{colLetter}{excelRow}" s="{styleId}" t="inlineStr"><is><t>{SecurityElement.Escape(cellVal)}</t></is></c>""");
                        }
                    }

                    sb.AppendLine("</row>");
                }

                sb.Append("""
                      </sheetData>
                    </worksheet>
                    """);

                WriteZipText(archive, $"xl/worksheets/sheet{s + 1}.xml", sb.ToString());
            }
        }

        return ms.ToArray();
    }

    private static void WriteZipText(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }

    [HttpGet("dtes")]
    public async Task<IActionResult> GetEmittedDtes(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        var dtes = await _dteRepository.GetByTenantAsync(tenantId, cancellationToken);

        var response = dtes.Select(d => new
        {
            Id = d.Id,
            SaleId = d.SaleId,
            DteType = (int)d.DteType,
            DteTypeName = d.DteType switch
            {
                DteType.FacturaElectronica => "Factura Electrónica (33)",
                DteType.BoletaElectronica => "Boleta Electrónica (39)",
                DteType.NotaCredito => "Nota de Crédito Electrónica (61)",
                _ => $"DTE ({(int)d.DteType})"
            },
            Folio = d.Folio,
            TotalAmount = d.TotalAmount,
            Status = (int)d.Status,
            StatusName = d.Status == DteStatus.Rejected ? "Anulado por NC (61)" : d.Status.ToString(),
            EmissionDate = d.EmissionDate,
            XmlContent = d.XmlContent
        });

        return Ok(response);
    }

    [HttpPost("dtes/{dteId:guid}/credit-note")]
    public async Task<IActionResult> EmitCreditNote(
        Guid dteId,
        [FromBody] EmitCreditNoteRequest? request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        var stockNotifications = new List<(Guid ProductId, int NewStock)>();

        try
        {
            var result = await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

                var originalDte = await _dbContext.DteDocuments
                    .FirstOrDefaultAsync(d => d.Id == dteId, cancellationToken);

                if (originalDte is null)
                {
                    throw new InvalidOperationException("El documento DTE seleccionado no existe.");
                }

                if (originalDte.DteType == DteType.NotaCredito)
                {
                    throw new InvalidOperationException("No se puede emitir una Nota de Crédito sobre otra Nota de Crédito.");
                }

                if (originalDte.Status == DteStatus.Rejected)
                {
                    throw new InvalidOperationException(
                        $"El documento Folio N° {originalDte.Folio} ya fue anulado previamente mediante una Nota de Crédito.");
                }

                var sale = await _dbContext.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == originalDte.SaleId, cancellationToken);

                var creditNoteFolio = await _dteRepository.GetNextFolioAsync(
                    tenantId: tenantId,
                    dteType: DteType.NotaCredito,
                    cancellationToken: cancellationToken);

                var motive = !string.IsNullOrWhiteSpace(request?.Reason)
                    ? request.Reason.Trim()
                    : $"Anula DTE Tipo {(int)originalDte.DteType} Folio N° {originalDte.Folio}";

                var creditNoteDte = DteDocument.Create(
                    tenantId: tenantId,
                    saleId: originalDte.SaleId,
                    dteType: DteType.NotaCredito,
                    folio: creditNoteFolio,
                    totalAmount: originalDte.TotalAmount);

                var emissionDateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var refDateStr = originalDte.EmissionDate.ToString("yyyy-MM-dd");
                var rutReceptor = !string.IsNullOrWhiteSpace(sale?.RutCliente) ? sale.RutCliente : "66.666.666-6";
                var razonReceptor = !string.IsNullOrWhiteSpace(sale?.RazonSocialCliente) ? sale.RazonSocialCliente : "Cliente POS";

                var creditNoteXml = $"""
                    <?xml version="1.0" encoding="ISO-8859-1"?>
                    <DTE version="1.0">
                      <Documento ID="F{creditNoteFolio}T61">
                        <Encabezado>
                          <IdDoc>
                            <TipoDTE>61</TipoDTE>
                            <Folio>{creditNoteFolio}</Folio>
                            <FchEmis>{emissionDateStr}</FchEmis>
                          </IdDoc>
                          <Receptor>
                            <RUTRecep>{rutReceptor}</RUTRecep>
                            <RznSocRecep>{razonReceptor}</RznSocRecep>
                          </Receptor>
                          <Totales>
                            <MntTotal>{originalDte.TotalAmount:0}</MntTotal>
                          </Totales>
                        </Encabezado>
                        <Referencia>
                          <NroLinRef>1</NroLinRef>
                          <TpoDocRef>{(int)originalDte.DteType}</TpoDocRef>
                          <FolioRef>{originalDte.Folio}</FolioRef>
                          <FchRef>{refDateStr}</FchRef>
                          <CodRef>1</CodRef>
                          <RazonRef>{motive}</RazonRef>
                        </Referencia>
                      </Documento>
                    </DTE>
                    """;

                creditNoteDte.MarkAsSigned(creditNoteXml);
                creditNoteDte.MarkAsSentToSii();
                creditNoteDte.MarkAsAccepted();

                originalDte.MarkAsAnnulledByCreditNote();

                await _dteRepository.AddAsync(creditNoteDte, cancellationToken);

                // Reingresar automáticamente las unidades vendidas al Stock y al Libro Kardex
                if (sale is not null && sale.Items.Count > 0)
                {
                    foreach (var item in sale.Items)
                    {
                        var qty = item.Quantity > 0
                            ? item.Quantity
                            : (int)Math.Round(item.Cantidad, 0, MidpointRounding.AwayFromZero);

                        if (item.ProductId != Guid.Empty && qty > 0)
                        {
                            var product = await _dbContext.Products
                                .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);

                            if (product is not null)
                            {
                                var currentPmp = product.PmpCost > 0m ? product.PmpCost : product.CostPrice;
                                var newStock = product.CurrentStock + qty;

                                product.Update(
                                    sku: product.Sku,
                                    name: product.Name,
                                    salePrice: product.SalePrice,
                                    costPrice: currentPmp,
                                    currentStock: newStock);

                                var kardexReentry = KardexTransaction.Create(
                                    tenantId: tenantId,
                                    productId: product.Id,
                                    movementType: MovementType.In,
                                    quantity: qty,
                                    unitCost: currentPmp,
                                    reason: $"[+{qty} u.] Reingreso por Nota de Crédito (61) Folio N° {creditNoteFolio} (Anula DTE {(int)originalDte.DteType} Folio N° {originalDte.Folio})");

                                await _dbContext.KardexTransactions.AddAsync(kardexReentry, cancellationToken);
                                stockNotifications.Add((product.Id, newStock));
                            }
                        }
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new
                {
                    CreditNoteId = creditNoteDte.Id,
                    CreditNoteFolio = creditNoteFolio,
                    AnnulledDteFolio = originalDte.Folio,
                    RestoredItemsCount = stockNotifications.Count,
                    Message = $"Nota de Crédito Electrónica (61) Folio N° {creditNoteFolio} emitida exitosamente. Stock reingresado al Kardex."
                };
            });

            foreach (var (productId, newStock) in stockNotifications)
            {
                await _inventoryNotifier.NotifyStockUpdateAsync(productId, newStock, cancellationToken);
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RegisterSale(
        [FromBody] RegisterSaleCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var saleId = await _mediator.Send(command, cancellationToken);
            return Ok(new { Id = saleId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
