import { ChangeDetectorRef, Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { OcrService } from '../../services/ocr-service';
import { InvoiceOcrResultDto } from '../../models/invoice-ocr-result.dto';
import { Router } from '@angular/router';

@Component({
  selector: 'app-history-nav',
  standalone: true,
  imports: [
    CommonModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule
  ],
  templateUrl: './history-nav.html',
  styleUrls: ['./history-nav.scss']
})
export class HistoryNavComponent {
  @Input() opened = false;
  
  protected _invoices: InvoiceOcrResultDto[] = [];
  loading = this._invoices.length === 0 ? true : false;
  private cd = inject(ChangeDetectorRef);
  private router = inject(Router);
  @Input() set invoices(value: InvoiceOcrResultDto[]) {
    this._invoices = value || [];
    this.loading = false;
    this.cd.markForCheck();
  }
  
  get invoices(): InvoiceOcrResultDto[] {
    return this._invoices;
  }

  @Output() invoiceSelected = new EventEmitter<InvoiceOcrResultDto>();

  constructor(private ocrServie: OcrService) {}

  ngOnInit(){
    this.ocrServie.invoices$.subscribe(invoices => {
      this.invoices = invoices;
    });
  }
  trackByFn(index: number, item: InvoiceOcrResultDto) {
  return item.id || index;
}

  toggle() {
    this.opened = !this.opened;
  }

  close() {
    this.opened = false;
  }

  selectInvoice(invoice: InvoiceOcrResultDto) {
    this.router.navigate(['/preview' , invoice.id]);
  }
}
