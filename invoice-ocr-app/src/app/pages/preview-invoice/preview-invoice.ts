import { Component, ElementRef, EventEmitter, Input, Output, ViewChild, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { InvoiceOcrResultDto } from '../../models/invoice-ocr-result.dto';
import { OcrService } from '../../services/ocr-service';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import html2pdf from 'html2pdf.js';
import { MatProgressSpinnerModule, MatSpinner } from '@angular/material/progress-spinner';
import { A } from '@angular/cdk/keycodes';

@Component({
  selector: 'app-preview-invoice',
  templateUrl: './preview-invoice.html',
  styleUrls: ['./preview-invoice.scss'],
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatTableModule,
    MatIconModule,
    MatProgressSpinnerModule,
    RouterLink
  ],
  standalone: true,
  providers: [DatePipe, CurrencyPipe]
})
export class PreviewInvoice implements OnInit {
  invoice: InvoiceOcrResultDto | null = null;
  loading = true;

  selectedTemplate: string = 'template1';

  templates = [
    { id: 'template1', name: 'Template1', previewImage: '/assets/templates/template-1.png' },
    { id: 'template2', name: 'Template2', previewImage: '/assets/templates/template-2.jpg' },
    { id: 'template3', name: 'Template3', previewImage: '/assets/templates/template-3.jpg' }
  ];

  @ViewChild('invoiceContent', { static: false }) invoiceContent!: ElementRef;

  constructor(
    private route: ActivatedRoute,
    private ocrService: OcrService,
    private cdRef : ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.loadInvoice(id);
      }
    });
  }

  selectTemplate(templateId: string): void {
    this.selectedTemplate = templateId;
    this.cdRef.detectChanges(); // Ensure template updates
  }

  loadInvoice(id: string): void {
    this.ocrService.getInvoiceById(id).subscribe({
      next: (invoice) => {
        this.invoice = invoice;
        this.loading = false;
      },
      error: (err) => {
        console.error('Failed to load invoice', err);
        this.loading = false;
      }
    });
  }

  get subtotal(): number {
    return this.invoice?.items?.reduce((sum, item) => sum + item.lineTotal, 0) || 0;
  }

  get total(): number {
    return this.invoice?.totalAmount || this.subtotal + (this.invoice?.vat || 0);
  }

  get formattedDate(): string {
    return this.invoice?.invoiceDate
      ? new Date(this.invoice.invoiceDate).toLocaleDateString()
      : 'N/A';
  }

  printInvoice() {
    if (!this.invoice || !this.invoiceContent) {
    console.warn('No invoice or content element found');
    return;
  }

  const content = this.invoiceContent.nativeElement;

  // Step 1: Find the actions element
  const actions = content.querySelector('.actions');
  

  // Step 2: Clone and remove buttons before export
  if (actions) {
    actions.style.display = 'none';
    
    window.print();
  }
  actions.style.display = '';
  
    
  }

  exportToPDF() {
  if (!this.invoice || !this.invoiceContent) {
    console.warn('No invoice or content element found');
    return;
  }

  const content = this.invoiceContent.nativeElement;

  // Step 1: Find the actions element
  const actions = content.querySelector('.actions');

  // Step 2: Clone and remove buttons before export
  if (actions) {
    actions.style.display = 'none';
  }

  const opt = {
    margin: 0.5,
    filename: `invoice_${this.invoice.invoiceNumber}.pdf`,
    image: { type: 'jpeg', quality: 0.98 },
    html2canvas: { scale: 2, useCORS: true },
    jsPDF: { unit: 'in', format: 'letter', orientation: 'portrait' }
  };

  // Step 3: Export PDF
  html2pdf()
    .from(content)
    .set(opt)
    .save()
    .then(() => {
      // Step 4: Restore the removed buttons after PDF is saved
      
        actions.style.display = '';
      
    })
    .catch((err: any) => {
      console.error('Error generating PDF', err);
      // Restore buttons even if PDF fails
      actions.style.display = '';
    });
}
}