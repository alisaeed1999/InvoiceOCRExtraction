export interface InvoiceItemDto {
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface InvoiceOcrResultDto {
  id : number | null;
  invoiceNumber: string;
  invoiceDate: string | null;
  customerName: string;
  totalAmount: number | null;
  vat: number | null;
  rawText: string | null;
  items: InvoiceItemDto[];
}
