import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PreviewInvoice } from './preview-invoice';
import { InvoiceOcrResultDto } from '../../models/invoice-ocr-result.dto';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

describe('PreviewInvoice', () => {
  let component: PreviewInvoice;
  let fixture: ComponentFixture<PreviewInvoice>;

  const mockInvoice: InvoiceOcrResultDto = {
    id: 1,
    invoiceNumber: 'INV-001',
    invoiceDate: new Date('2023-01-01').toISOString(),
    customerName: 'Customer A',
    totalAmount: 110,
    vat: 10,
    rawText: '',
    items: [
      { description: 'Item 1', quantity: 2, unitPrice: 50, lineTotal: 100 }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        CommonModule,
        MatButtonModule,
        MatCardModule,
        MatTableModule,
        MatIconModule,
        MatProgressSpinnerModule
      ],
      declarations: [PreviewInvoice]
    }).compileComponents();

    fixture = TestBed.createComponent(PreviewInvoice);
    component = fixture.componentInstance;
    component.ocrResult = mockInvoice;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should calculate subtotal correctly', () => {
    expect(component.subtotal).toBe(100);
  });

  it('should calculate total correctly', () => {
    expect(component.total).toBe(110);
  });

  it('should format date correctly', () => {
    const dateStr = mockInvoice.invoiceDate || '';
    expect(component.formattedDate).toBe(new Date(dateStr).toLocaleDateString());
  });

  it('should have displayed columns', () => {
    expect(component.displayedColumns).toEqual(['description', 'quantity', 'unitPrice', 'lineTotal']);
  });

  it('should have invoiceContent ViewChild defined', () => {
    expect(component.invoiceContent).toBeDefined();
  });

  // Additional tests for printInvoice and exportToPDF can be added if needed
});
