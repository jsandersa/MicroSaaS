export interface ProductDTO {
  id: string;
  name: string;
  sku: string;
  price: number;
  stock: number;
  customMarginPercentage: number | null;
}


export interface SaleItem {
  product: ProductDTO;
  quantity: number;
  subtotal: number;
}

export interface CreateSaleCommand {
  items: SaleItem[];
  totalAmount: number;
  customerRut: string;
}

