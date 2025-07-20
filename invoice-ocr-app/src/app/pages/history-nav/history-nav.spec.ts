import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HistoryNavComponent } from './history-nav';
import { OcrService } from '../../services/ocr-service';
import { of } from 'rxjs';
import { By } from '@angular/platform-browser';

describe('HistoryNavComponent', () => {
  let component: HistoryNavComponent;
  let fixture: ComponentFixture<HistoryNavComponent>;
  let ocrServiceSpy: jasmine.SpyObj<OcrService>;

  const mockInvoices = [
    { id: 1, invoiceNumber: 'INV-001', customerName: 'Customer A', totalAmount: 100 },
    { id: 2, invoiceNumber: 'INV-002', customerName: 'Customer B', totalAmount: 200 }
  ];

  beforeEach(async () => {
    const ocrSpy = jasmine.createSpyObj('OcrService', ['getInvoices']);
    ocrSpy.getInvoices.and.returnValue(of(mockInvoices));

    await TestBed.configureTestingModule({
      declarations: [HistoryNavComponent],
      providers: [
        { provide: OcrService, useValue: ocrSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(HistoryNavComponent);
    component = fixture.componentInstance;
    ocrServiceSpy = TestBed.inject(OcrService) as jasmine.SpyObj<OcrService>;

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load invoices on init', () => {
    expect(component.invoices.length).toBe(2);
  });

  it('should display invoice numbers in the template', () => {
    const invoiceElements = fixture.debugElement.queryAll(By.css('.invoice-item'));
    expect(invoiceElements.length).toBe(2);
    expect(invoiceElements[0].nativeElement.textContent).toContain('INV-001');
    expect(invoiceElements[1].nativeElement.textContent).toContain('INV-002');
  });

  // Additional tests for interaction can be added here
});
