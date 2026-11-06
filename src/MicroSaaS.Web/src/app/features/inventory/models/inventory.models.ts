export interface ProductInventoryDTO {
  id: string;
  sku: string;
  name: string;
  currentStock: number;
  minimumStock: number;
  price: number;
  pmpCost: number;
  category: string;
}

export interface CreateProductCommand {
  sku: string;
  name: string;
  initialStock: number;
  minimumStock: number;
  price: number;
  category: string;
}

export interface KardexMovementDTO {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  currentPmpCost: number;
  movementType: number;
  movementTypeName: string;
  quantity: number;
  unitCost: number;
  totalValue: number;
  transactionDate: string;
  reason?: string;
}
