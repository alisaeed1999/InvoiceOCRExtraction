using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using InvoiceOCRApp.Models;

namespace InvoiceOCRApp.Data;

public partial class InvoiceAppContext : DbContext
{
    public InvoiceAppContext()
    {
    }

    public InvoiceAppContext(DbContextOptions<InvoiceAppContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<Invoicedetail> Invoicedetails { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoices_pkey");

            entity.ToTable("invoices");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Customername)
                .HasMaxLength(200)
                .HasColumnName("customername");
            entity.Property(e => e.Invoicedate).HasColumnName("invoicedate");
            entity.Property(e => e.Invoicenumber)
                .HasMaxLength(100)
                .HasColumnName("invoicenumber");
            entity.Property(e => e.Totalamount)
                .HasPrecision(10, 2)
                .HasColumnName("totalamount");
            entity.Property(e => e.Vat)
                .HasPrecision(10, 2)
                .HasColumnName("vat");

            
        });

        modelBuilder.Entity<Invoicedetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoicedetails_pkey");

            entity.ToTable("invoicedetails");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Invoiceid).HasColumnName("invoiceid");
            entity.Property(e => e.Linetotal)
                .HasPrecision(10, 2)
                .HasColumnName("linetotal");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Unitprice)
                .HasPrecision(10, 2)
                .HasColumnName("unitprice");

            entity.HasOne(d => d.Invoice).WithMany(p => p.Invoicedetails)
                .HasForeignKey(d => d.Invoiceid)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("invoicedetails_invoiceid_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
