import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Header } from './pages/header/header';
import { Footer } from './pages/footer/footer';
import { HistoryNavComponent } from './pages/history-nav/history-nav';
import { MatSidenavModule } from '@angular/material/sidenav';
import { InvoiceOcrResultDto } from './models/invoice-ocr-result.dto';
import { OcrService } from './services/ocr-service';
import { HttpClientModule } from '@angular/common/http';
import { JsonPipe, NgIf } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone : true,
  imports: [RouterOutlet ,
     Header , 
     Footer, 
     HistoryNavComponent,
    MatSidenavModule,
    JsonPipe,
  NgIf],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected title = 'invoice-ocr-app';
  showHistory = false;
  
  invoices: InvoiceOcrResultDto[] = [];
  
  private ocrService = inject(OcrService)
  
  toggleHistory() {
    this.showHistory = !this.showHistory;
  }

  ngOnInit() {
    this.loadInvoices();
  }

  loadInvoices() {
    this.ocrService.getAllInvoices().subscribe({
      next: (data) => {
        this.invoices = data;
      },
      error: (err) => {
        console.error('Failed to load invoices', err);
      }
    });
  }

  onInvoiceSelected(invoice: InvoiceOcrResultDto) {
    console.log('Invoice selected in App:', invoice);
    // You can emit this to a parent component or route
  }
}
