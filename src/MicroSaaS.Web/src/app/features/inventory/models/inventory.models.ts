export interface ProductInventoryDTO {
  id: string;
  sku: string;
  name: string;
  currentStock: number;
  minimumStock: number;
  price: number;
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

