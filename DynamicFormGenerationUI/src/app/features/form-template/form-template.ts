import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import {FormService} from "../../core/services/form";
import { CreateFormTemplateRequest, FormListItem } from "../../core/models/form.model";
import { CreateFormTemplate } from '../create-form-template/create-form-template';

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

    constructor(private iobjFormService: FormService){}
    
    ngOnInit():void
    {
        this.search();
    }

    createFormTemplate()
    {
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
                this.isCreateTemplateVisible = false;
                this.clearFilters();
            },
            error: (error) => console.error('Unable to create form template:', error)
        });
    }
    // Close Create Form Template dialog
    closeCreateTemplate(): void 
    {
         this.isCreateTemplateVisible = false;
    }
    onFilterChange(){
        this.search();
    }  
}