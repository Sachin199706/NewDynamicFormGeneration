import { CommonModule } from '@angular/common';
import {Component, inject, Input, OnChanges, output, SimpleChanges} from "@angular/core";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormListItem } from '../../core/models/form.model';

@Component({
  selector: "app-create-form-template",
  templateUrl: "./create-form-template.html",
  styleUrl: "./create-form-template.scss",
  imports: [CommonModule, ReactiveFormsModule]
})

export class CreateFormTemplate implements OnChanges {
    @Input() template: FormListItem | null = null;
    private fb: FormBuilder = inject(FormBuilder);
    templateForm: FormGroup = this.fb.group({
        formName:['', Validators.required],
        formCode:['', Validators.required],
        description:['']
    });
    // Send the created template to the parent component
    templateCreated = output<{ formName: string; formCode: string; description?: string }>();
    templateUpdated = output<{ formId: number; formName: string; formCode: string; description?: string }>();
    closed = output<void>();

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['template']) {
            if (this.template) {
                this.templateForm.patchValue(this.template);
            } else {
                this.templateForm.reset();
            }
        }
    }

    createTemplate():void{
        this.templateForm.markAllAsTouched();
        if(this.templateForm.invalid)
        {
            return;
        }
        const lobjTemplate = this.templateForm.getRawValue();
        if (this.template) {
            this.templateUpdated.emit({ formId: this.template.formId, ...lobjTemplate });
        } else {
            this.templateCreated.emit(lobjTemplate);
        }
        this.templateForm.reset();
    }
    close(): void { // Parent can handle closing the dialog/modal 
       this.templateForm.reset();
       this.closed.emit();
    }
    isInvalid(controlName: string): boolean 
    {
         const lobjControl = this.templateForm.get(controlName); 
         return !!( lobjControl && lobjControl.invalid && (lobjControl.touched || lobjControl.dirty) );
    } 
}
