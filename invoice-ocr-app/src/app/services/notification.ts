// import { Injectable } from '@angular/core';
// import { MatSnackBar } from '@angular/material/snack-bar';

// @Injectable({ providedIn: 'root' })
// export class NotificationService {
//   constructor(private snackBar: MatSnackBar) {}

//   success(message: string): void {
//     this.snackBar.open(message, 'Close', { duration: 3000, panelClass: 'snackbar-success' });
//   }

//   error(message: string): void {
//     this.snackBar.open(message, 'Close', { duration: 4000, panelClass: 'snackbar-error' });
//   }
// }



import { Injectable } from '@angular/core';
import Swal from 'sweetalert2';
// import { SwalService } from '@sweetalert2/ng-sweetalert2';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  constructor() {}

  success(message: string): void {
    Swal.fire({
      icon: 'success',
      title: 'Success',
      text: message,
      timer: 3000,
      showConfirmButton: false,
    });
  }

  error(message: string): void {
    Swal.fire({
      icon: 'error',
      title: 'Error',
      text: message,
      timer: 4000,
      showConfirmButton: true,
      confirmButtonText: 'Close',
      timerProgressBar: true,
    });
  }

  // Optional: show confirmation dialog
  confirm(title: string, text: string): Promise<any> {
    return Swal.fire({
      title,
      text,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes',
      cancelButtonText: 'No'
    });
  }

  // Optional: info toast or modal
  info(message: string): void {
    Swal.fire({
      icon: 'info',
      title: 'Info',
      text: message,
      timer: 3000,
      showConfirmButton: false
    });
  }
}