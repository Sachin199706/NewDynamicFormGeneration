import { CommonModule } from '@angular/common';
import {Component,inject, output} from "@angular/core";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  selector: "app-create-form-template",
  templateUrl: "./create-form-template.html",
  styleUrl: "./create-form-template.scss",
  imports:[CommonModule, ReactiveFormsModule]
})

export class CreateFormTemplate {
    private fb: FormBuilder = inject(FormBuilder);
    templateForm: FormGroup = this.fb.group({
        formName:['', Validators.required],
        formCode:['', Validators.required],
        description:['']
    });
    // Send the created template to the parent component
    templateCreated = output<any>();
    closed = output<void>();

    createTemplate():void{
        this.templateForm.markAllAsTouched();
        if(this.templateForm.invalid)
        {
            return;
        }
        const lobjTemplate = this.templateForm.getRawValue();
        console.log('Creating Form Template:', lobjTemplate);
        this.templateCreated.emit(lobjTemplate);
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
