import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EditInvoice } from './edit-invoice';
import { ReactiveFormsModule, FormGroup, FormControl, FormArray } from '@angular/forms';
import { By } from '@angular/platform-browser';

describe('EditInvoice', () => {
  let component: EditInvoice;
  let fixture: ComponentFixture<EditInvoice>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReactiveFormsModule],
      declarations: [EditInvoice]
    }).compileComponents();

    fixture = TestBed.createComponent(EditInvoice);
    component = fixture.componentInstance;

    // Initialize a form group for input
    component.invoiceForm = new FormGroup({
      invoiceNumber: new FormControl('INV-001'),
      invoiceDate: new FormControl(new Date()),
      customerName: new FormControl('Customer A'),
      totalAmount: new FormControl(100),
      vat: new FormControl(10),
      manualEdit: new FormControl(true),
      items: new FormArray([
        new FormGroup({
          description: new FormControl('Item 1'),
          quantity: new FormControl(2),
          unitPrice: new FormControl(20),
          lineTotal: new FormControl({ value: 40, disabled: true })
        })
      ])
    });

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have one item in items FormArray', () => {
    expect(component.items.length).toBe(1);
  });

  it('should add and remove items', () => {
    component.addItem();
    expect(component.items.length).toBe(2);
    component.removeItem(1);
    expect(component.items.length).toBe(1);
  });

  it('should update line total correctly', () => {
    const item = component.items.at(0);
    item.get('quantity')?.setValue(3);
    item.get('unitPrice')?.setValue(10);
    component.updateLineTotal(item);
    expect(item.get('lineTotal')?.value).toBe(30);
  });

  it('should toggle preview mode and disable/enable form', () => {
    component.togglePreviewMode();
    expect(component.previewMode).toBeTrue();
    expect(component.invoiceForm.disabled).toBeTrue();
    component.togglePreviewMode();
    expect(component.previewMode).toBeFalse();
    expect(component.invoiceForm.enabled).toBeTrue();
  });
});
