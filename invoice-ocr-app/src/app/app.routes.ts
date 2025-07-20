import { Routes } from '@angular/router';
import { UploadInvoice } from './pages/upload-invoice/upload-invoice';
import { PreviewInvoice } from './pages/preview-invoice/preview-invoice';

export const routes: Routes = [
     { path: '', component: UploadInvoice },
     { path: 'preview/:id', component: PreviewInvoice }
];
