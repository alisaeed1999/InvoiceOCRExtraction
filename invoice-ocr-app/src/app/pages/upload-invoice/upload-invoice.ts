import { Component } from '@angular/core';
import { FormBuilder, FormArray, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';

import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { OcrService } from '../../services/ocr-service';
import { InvoiceOcrResultDto } from '../../models/invoice-ocr-result.dto';
import { EditInvoice } from '../edit-invoice/edit-invoice';

import { finalize } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { NotificationService } from '../../services/notification';


@Component({
  selector: 'app-upload-invoice',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatCardModule,
    EditInvoice,
    
  ],
  templateUrl: './upload-invoice.html',
  styleUrls: ['./upload-invoice.scss']
})
export class UploadInvoice {
  invoiceForm: FormGroup;
  selectedFile: File | null = null;
  loading = false;
  ocrResult: InvoiceOcrResultDto | null = null;
  isDragging = false;
  showHistoryPreview = false;
  historyInvoice: InvoiceOcrResultDto | null = null;

  constructor(
    private fb: FormBuilder, 
    private ocrService: OcrService,
    private notification: NotificationService,
    private router: Router
  ) {
    this.invoiceForm = this.createForm();
  }

  createForm(): FormGroup {
    return this.fb.group({
      invoiceNumber: ['', Validators.required],
      invoiceDate: [null, Validators.required],
      customerName: ['', [Validators.required, Validators.maxLength(200)]],
      totalAmount: [0, [Validators.required, Validators.min(0)]],
      vat: [0, [Validators.required, Validators.min(0)]],
      manualEdit: [false],
      items: this.fb.array([this.createItem()])
    });
  }

  createItem(): FormGroup {
    return this.fb.group({
      description: ['', Validators.required],
      quantity: [0, [Validators.required, Validators.min(0)]],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
      lineTotal: [{ value: 0, disabled: true }]
    });
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = true;
  }

  onDragLeave(): void {
    this.isDragging = false;
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = false;
    
    if (event.dataTransfer?.files) {
      const files = event.dataTransfer.files;
      if (files.length > 0) {
        this.selectedFile = files[0];
        this.ocrResult = null;
        this.invoiceForm.patchValue({ manualEdit: false });
      }
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.selectedFile = input.files[0];
      this.ocrResult = null;
      this.invoiceForm.patchValue({ manualEdit: false });
    }
  }

  onUpload(): void {
    if (!this.selectedFile) return;
    
    this.loading = true;
    this.ocrService.uploadInvoice(this.selectedFile).pipe(
      finalize(() => this.loading = false)
    ).subscribe({
      next: (res) => {
        this.ocrResult = res;
        this.notification.success('Invoice processed successfully');
        this.populateFormFromOcrResult();
      },
      error: (err) => {
        console.error('OCR Processing Error:', err);
        this.notification.error('Failed to process invoice. Please try again or enter details manually.');
      }
    });
  }

  populateFormFromOcrResult(): void {
    if (!this.ocrResult) return;
    
    // Clear existing items
    const itemsArray = this.invoiceForm.get('items') as FormArray;
    while (itemsArray.length) {
      itemsArray.removeAt(0);
    }

    // Patch main form
    this.invoiceForm.patchValue({
      invoiceNumber: this.ocrResult.invoiceNumber || '',
      invoiceDate: this.ocrResult.invoiceDate ? new Date(this.ocrResult.invoiceDate) : null,
      customerName: this.ocrResult.customerName || '',
      totalAmount: this.ocrResult.totalAmount || 0,
      vat: this.ocrResult.vat || 0
    });

    // Add items
    if (this.ocrResult.items?.length) {
      this.ocrResult.items.forEach(item => {
        const itemGroup = this.createItem();
        itemGroup.patchValue({
          description: item.description || '',
          quantity: item.quantity || 0,
          unitPrice: item.unitPrice || 0,
          lineTotal: item.lineTotal || 0
        });
        itemsArray.push(itemGroup);
      });
    } else {
      itemsArray.push(this.createItem());
    }
    
    // Enable edit mode
    this.invoiceForm.patchValue({ manualEdit: true });
  }

  onSubmit(): void {
    if (this.invoiceForm.invalid) {
      this.notification.error('Please fill in all required fields');
      return;
    }
    
    this.loading = true;
    const formData = this.invoiceForm.getRawValue();
    
    // Format date as yyyy-MM-dd
    const formattedDate = formData.invoiceDate ? 
      new Date(formData.invoiceDate).toISOString().split('T')[0] : 
      null;

    const invoiceData : InvoiceOcrResultDto = {
      invoiceNumber: formData.invoiceNumber,
      invoiceDate: formattedDate,
      customerName: formData.customerName,
      totalAmount: formData.totalAmount,
      vat: formData.vat,
      items: formData.items,
      id: null,
      rawText: null
    };
    
    
    this.ocrService.saveInvoice(invoiceData).pipe(
      finalize(() => this.loading = false)
    ).subscribe({
      next: (res) => {
        this.notification.success(`Invoice saved successfully with ID: ${res.id}`);
        // Navigate to preview route
        this.router.navigate(['/preview', res.id]);
      },
      error: (err) => {
        console.error('Save Error:', err);
        this.notification.error('Error saving invoice. Please try again.');
      }
    });
  }

  resetForm(): void {
    this.invoiceForm = this.createForm();
    this.selectedFile = null;
    this.ocrResult = null;
    this.isDragging = false;
  }

  onInvoiceSelected(invoice: InvoiceOcrResultDto): void {
    this.historyInvoice = invoice;
    this.showHistoryPreview = true;
  }
}