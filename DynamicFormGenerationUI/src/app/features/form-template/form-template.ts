import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';

import {FormService} from "../../core/services/form";
import { CreateFormTemplateRequest, FormListItem } from "../../core/models/form.model";
import { CreateFormTemplate } from '../create-form-template/create-form-template';
import { ToastrService } from 'ngx-toastr';


@Component({
selector:"app-form-template",
imports:[CommonModule, FormsModule, RouterLink, CreateFormTemplate],
templateUrl:"../form-template/form-template.html",
styleUrl:"../form-template/form-template.scss"
})

export class FormTemplate implements OnInit{
    iarrForms: FormListItem[] = [];
    strSearch:string = "";
    dtFromDate: string | null = null;
    dtToDate: string | null = null;
   // Controls Create Form Template dialog
    isCreateTemplateVisible: boolean = false;
    editingTemplate: FormListItem | null = null;

    constructor(private iobjFormService: FormService, private toastr: ToastrService, private router: Router){}
    
    ngOnInit():void
    {
        this.search();
    }

    createFormTemplate()
    {
        this.editingTemplate = null;
        this.isCreateTemplateVisible = true;
    }

    editFormTemplate(template: FormListItem): void
    {
        this.editingTemplate = template;
        this.isCreateTemplateVisible = true;
    }

    search()
    {
        this.iobjFormService.getForms(1, 10, this.strSearch, this.dtFromDate, this.dtToDate).subscribe(res => { this.iarrForms = res.items; });
    }

   clearFilters(): void { 
      this.strSearch = '';
      this.dtFromDate = null; 
      this.dtToDate = null; 
      this.search();
   }

    // Called when template is created 
    onTemplateCreated(template: CreateFormTemplateRequest): void 
    { 
        console.log('Template Created:', template);
        this.iobjFormService.createTemplate(template).subscribe({
            next: () => {
                this.toastr.success('Form template created successfully!', 'Success');
                this.isCreateTemplateVisible = false;
                this.clearFilters();
            },
            error: (error) => {
                this.toastr.error('Unable to create form template.', 'Error');
                console.error('Unable to create form template:', error);
            }
        });
    }

    onTemplateUpdated(template: CreateFormTemplateRequest & { formId: number }): void
    {
        this.iobjFormService.updateTemplate(template.formId, template).subscribe({
            next: () => {
                this.toastr.success('Form template updated successfully!', 'Success');
                this.isCreateTemplateVisible = false;
                this.editingTemplate = null;
                this.clearFilters();
            },
            error: (error) => 
            {
                this.toastr.error('Unable to update form template ', "Error"); 
                console.error('Unable to update form template:', error)
            }
          });
    }
    // Close Create Form Template dialog
    closeCreateTemplate(): void 
    {
         this.isCreateTemplateVisible = false;
            this.editingTemplate = null;
    }
    onFilterChange(){
        this.search();
    }  
    openVersions(formListItem: FormListItem): void {
        // Navigate to the versions page for the selected form template
        this.router.navigate(['/formtemplates', formListItem.formId, 'versions'], {
            queryParams:{tname: formListItem.formName}
        });
    }
}