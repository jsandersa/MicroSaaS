export interface SupplierDTO {
  id: string;
  rut: string;
  name: string;
}

export interface PurchaseItem {
  productId: string;
  name: string;
  sku: string;
  quantity: number;
  unitCost: number;
  subtotal: number;
}

export interface CreatePurchaseCommand {
  supplierRut: string;
  documentNumber: string;
  items: PurchaseItem[];
  totalAmount: number;
}

export interface PurchaseProductOption {
  id: string;
  sku: string;
  name: string;
  unitCost: number;
  currentStock: number;
}

