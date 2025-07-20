# Invoice OCR Application

This project is an Invoice OCR Extraction application built with ASP.NET 9 for the backend and Angular 20 for the frontend. It uses a database-first approach with PostgreSQL as the database.

---

## Technologies Used

- **Backend:** ASP.NET 9
- **Frontend:** Angular 20
- **Database:** PostgreSQL (Database First Approach)

---

## Installation and Setup

### Backend (ASP.NET 9)

1. **Prerequisites:**
   - [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
   - PostgreSQL database installed and running  
     - Install PostgreSQL from https://www.postgresql.org/download/ and start the PostgreSQL service.  
     - Create a PostgreSQL user with appropriate permissions.

2. **Restore NuGet Packages:**

   The project uses the following NuGet packages:

   - Ghostscript.NET (v1.3.0)
   - IronOcr (v2025.6.4)
   - IronOcr.Linux (v2025.6.4)
   - Microsoft.AspNetCore.OpenApi (v9.0.6)
   - Microsoft.EntityFrameworkCore.Design (v9.0.7)
   - Microsoft.Recognizers.Text.DataTypes.TimexExpression (v1.8.13)
   - Microsoft.Recognizers.Text.DateTime (v1.8.13)
   - Npgsql.EntityFrameworkCore.PostgreSQL (v9.0.4)
   - SixLabors.ImageSharp (v3.1.10)
   - Swashbuckle.AspNetCore (v9.0.3)
   - Tesseract (v5.2.0)
   - TesseractOCR (v5.5.1)

   To add these packages manually, use the following commands in the backend project directory (`InvoiceOCRApp`):

   ```bash
   dotnet add package Ghostscript.NET --version 1.3.0
   dotnet add package IronOcr --version 2025.6.4
   dotnet add package IronOcr.Linux --version 2025.6.4
   dotnet add package Microsoft.AspNetCore.OpenApi --version 9.0.6
   dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.7
   dotnet add package Microsoft.Recognizers.Text.DataTypes.TimexExpression --version 1.8.13
   dotnet add package Microsoft.Recognizers.Text.DateTime --version 1.8.13
   dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 9.0.4
   dotnet add package SixLabors.ImageSharp --version 3.1.10
   dotnet add package Swashbuckle.AspNetCore --version 9.0.3
   dotnet add package Tesseract --version 5.2.0
   dotnet add package TesseractOCR --version 5.5.1
   ```

3. **Configure PostgreSQL Connection String:**

   The connection string is located in `appsettings.json`:

   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Database=InvoiceApp;Username=postgres;Password=YourPassword"
   }
   ```

   Replace `Host`, `Database`, `Username`, and `Password` with your PostgreSQL server details.

4. **Create Database Schema:**

   - Use Entity Framework Core migrations located in the `InvoiceOCRApp/Migrations/` folder to create the database schema.  
   - Run the following command in the `InvoiceOCRApp` directory to apply migrations and create tables:  
   ```bash
   dotnet ef database update
   ```

5. **Ensure tessdata Folder is Present:**

   - The `tessdata` folder contains OCR language data files required by the OCR engine.  
   - Verify that the `InvoiceOCRApp/tessdata/` folder exists and contains files like `eng.traineddata`.  
   - These files are necessary for OCR functionality.

6. **Run the Backend:**

   From the `InvoiceOCRApp` directory, run:

   ```bash
   dotnet restore
   dotnet build
   dotnet run
   ```

---

### Frontend (Angular 20)

1. **Prerequisites:**
   - [Node.js](https://nodejs.org/) (Recommended version 18 or later)
   - [Angular CLI](https://angular.io/cli)

2. **Install Dependencies:**

   Navigate to the `invoice-ocr-app` directory and run:

   ```bash
   npm install
   ```

3. **Run the Frontend:**

   ```bash
   ng serve
   ```

   The app will be available at `http://localhost:4200`.

---

## Database First Approach

This project uses Entity Framework Core with a database-first approach. The database schema is created and managed in PostgreSQL, and the Entity Framework models are generated from the existing database.

---

## Additional Notes

- Make sure PostgreSQL is running and accessible before starting the backend.
- Update the connection string in `appsettings.json` to match your PostgreSQL credentials.
- The backend exposes APIs consumed by the Angular frontend.

---

This README provides a basic overview of installation and setup. For more detailed instructions, please refer to the project documentation or contact the project maintainer.
