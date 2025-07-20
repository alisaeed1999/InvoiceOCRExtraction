import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, tap, timeout } from 'rxjs';
import { InvoiceItemDto, InvoiceOcrResultDto } from '../models/invoice-ocr-result.dto';
import { environment } from '../../environment';

@Injectable({
  providedIn: 'root'
})
export class OcrService {
  private readonly apiUrl = environment.apiUrl; // change if needed

  private invoicesSubject = new BehaviorSubject<InvoiceOcrResultDto[]>([]);
invoices$ = this.invoicesSubject.asObservable();


  constructor(private http: HttpClient) {}

  uploadInvoice(file: File): Observable<InvoiceOcrResultDto> {
    const formData = new FormData();
    formData.append('file', file);
    
    return this.http.post<InvoiceOcrResultDto>(`${this.apiUrl}/upload`, formData)
  }

  saveInvoice(invoice: InvoiceOcrResultDto) {
  return this.http.post<{ id: number }>(`${this.apiUrl}/save`, invoice).pipe(
    tap(() => this.getAllInvoices().subscribe(
      invoices => {
        this.invoicesSubject.next(invoices);
      }
    ))
  );
}

// Get all invoices from the backend
  getAllInvoices(): Observable<InvoiceOcrResultDto[]> {
    return this.http.get<InvoiceOcrResultDto[]>(this.apiUrl);
  }

  // Get a specific invoice by ID
  getInvoiceById(id: string): Observable<InvoiceOcrResultDto> {
    return this.http.get<InvoiceOcrResultDto>(`${this.apiUrl}/${id}`);
  }
}
