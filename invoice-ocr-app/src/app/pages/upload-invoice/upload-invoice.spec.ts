import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UploadInvoice } from './upload-invoice';
import { ReactiveFormsModule, FormGroup, FormControl, FormArray } from '@angular/forms';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { OcrService } from '../../services/ocr-service';
import { of, throwError } from 'rxjs';
import { NotificationService } from '../../services/notification';

describe('UploadInvoice', () => {
  let component: UploadInvoice;
  let fixture: ComponentFixture<UploadInvoice>;
  let ocrServiceSpy: jasmine.SpyObj<OcrService>;
  let notificationSpy: jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    const ocrSpy = jasmine.createSpyObj('OcrService', ['uploadInvoice', 'saveInvoice', 'getInvoiceById']);
    const notifSpy = jasmine.createSpyObj('NotificationService', ['success', 'error']);

    await TestBed.configureTestingModule({
      imports: [ReactiveFormsModule, HttpClientTestingModule, UploadInvoice],
      providers: [
        { provide: OcrService, useValue: ocrSpy },
        { provide: NotificationService, useValue: notifSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UploadInvoice);
    component = fixture.componentInstance;
    ocrServiceSpy = TestBed.inject(OcrService) as jasmine.SpyObj<OcrService>;
    notificationSpy = TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>;

    // Initialize form
    component.invoiceForm = new FormGroup({
      invoiceNumber: new FormControl(''),
      invoiceDate: new FormControl(null),
      customerName: new FormControl(''),
      totalAmount: new FormControl(0),
      vat: new FormControl(0),
      manualEdit: new FormControl(false),
      items: new FormArray([])
    });
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should upload file and set ocrResult on success', () => {
    const mockFile = new File(['dummy content'], 'invoice.pdf', { type: 'application/pdf' });
    component.selectedFile = mockFile;
    const mockOcrResult = { id: 1, invoiceNumber: '123', invoiceDate: '2023-01-01', customerName: 'Test', totalAmount: 100, vat: 10, items: [], rawText: '' };
    ocrServiceSpy.uploadInvoice.and.returnValue(of(mockOcrResult));

    component.onUpload();

    expect(ocrServiceSpy.uploadInvoice).toHaveBeenCalledWith(mockFile);
  });

  it('should handle upload error', () => {
    const mockFile = new File(['dummy content'], 'invoice.pdf', { type: 'application/pdf' });
    component.selectedFile = mockFile;
    ocrServiceSpy.uploadInvoice.and.returnValue(throwError(() => new Error('Upload failed')));

    component.onUpload();

    expect(notificationSpy.error).toHaveBeenCalledWith('Failed to process the invoice. Please try again or enter details manually.');
  });

  it('should save invoice and show preview on success', () => {
    const mockSaveResponse = { id: 1 };
    const mockSavedInvoice = { id: 1, invoiceNumber: '123', invoiceDate: '2023-01-01', customerName: 'Test', totalAmount: 100, vat: 10, items: [], rawText: '' };
    ocrServiceSpy.saveInvoice.and.returnValue(of(mockSaveResponse));
    ocrServiceSpy.getInvoiceById.and.returnValue(of(mockSavedInvoice));

    component.invoiceForm.patchValue({
      invoiceNumber: '123',
      invoiceDate: new Date('2023-01-01'),
      customerName: 'Test',
      totalAmount: 100,
      vat: 10,
      manualEdit: true,
      items: []
    });

    component.onSubmit();

    expect(ocrServiceSpy.saveInvoice).toHaveBeenCalled();
    expect(notificationSpy.success).toHaveBeenCalledWith('Invoice saved successfully with ID : 1');
    expect(component.showPreview).toBeTrue();
  });

  it('should handle save error', () => {
    ocrServiceSpy.saveInvoice.and.returnValue(throwError(() => new Error('Save failed')));

    component.onSubmit();

    expect(notificationSpy.error).toHaveBeenCalledWith('Error saving invoice. Please try again.');
  });
});
