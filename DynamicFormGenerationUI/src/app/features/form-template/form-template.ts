import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';

import {FormService} from "../../core/services/form";
import { CreateFormTemplateRequest, FormListItem } from "../../core/models/form.model";
import { CreateFormTemplate } from '../create-form-template/create-form-template';
import { ToastrService } from 'ngx-toastr';
import { Pagination } from '../pagination/pagination';


@Component({
selector:"app-form-template",
imports:[CommonModule, FormsModule, RouterLink, CreateFormTemplate, Pagination],
templateUrl:"../form-template/form-template.html",
styleUrl:"../form-template/form-template.scss"
})

export class FormTemplate implements OnInit, OnDestroy{
    iarrForms: FormListItem[] = [];
    strSearch:string = "";
    dtFromDate: string | null = null;
    dtToDate: string | null = null;
    inumCurrentPage = 1;
    inumPageSize = 10;
    inumTotalCount = 0;
    inumTotalPages = 0;
    Math = Math;
    private searchTimer: ReturnType<typeof setTimeout> | undefined;
    private loadRequestId = 0;
   // Controls Create Form Template dialog
    isCreateTemplateVisible: boolean = false;
    editingTemplate: FormListItem | null = null;

    constructor(private iobjFormService: FormService, private toastr: ToastrService, private router: Router){}
    
    ngOnInit():void
    {
        this.search();
    }

    ngOnDestroy(): void {
        this.clearSearchTimer();
        this.loadRequestId++;
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
        this.clearSearchTimer();
        this.inumCurrentPage = 1;
        this.loadForms();
    }

   clearFilters(): void { 
      this.strSearch = '';
      this.dtFromDate = null; 
      this.dtToDate = null; 
      this.search();
   }

    onFilterChange(): void {
        this.inumCurrentPage = 1;
        this.loadRequestId++;
        this.clearSearchTimer();
        this.searchTimer = setTimeout(() => {
            this.searchTimer = undefined;
            this.loadForms();
        }, 250);
    }

    onPageSizeChange(): void {
        this.clearSearchTimer();
        this.inumCurrentPage = 1;
        this.loadForms();
    }

    goToPage(aNumPage: number): void {
        if (aNumPage < 1 || aNumPage > this.inumTotalPages || aNumPage === this.inumCurrentPage) {
            return;
        }

        this.clearSearchTimer();
        this.inumCurrentPage = aNumPage;
        this.loadForms();
    }

    private loadForms(): void {
        const lnumRequestId = ++this.loadRequestId;
        this.iobjFormService.getForms(
            this.inumCurrentPage,
            this.inumPageSize,
            this.strSearch,
            this.dtFromDate,
            this.dtToDate
        ).subscribe({
            next: (res) => {
                if (lnumRequestId !== this.loadRequestId) {
                    return;
                }

                if (res.totalPages > 0 && this.inumCurrentPage > res.totalPages) {
                    this.inumCurrentPage = res.totalPages;
                    this.loadForms();
                    return;
                }

                this.inumTotalCount = res.totalCount;
                this.inumTotalPages = res.totalPages;
                if (res.totalPages === 0) {
                    this.inumCurrentPage = 1;
                }
                this.iarrForms = res.items;
            },
            error: () => {
                if (lnumRequestId !== this.loadRequestId) {
                    return;
                }

                this.iarrForms = [];
                this.inumTotalCount = 0;
                this.inumTotalPages = 0;
                this.toastr.error('Unable to load form templates.', 'Error');
            }
        });
    }

    private clearSearchTimer(): void {
        if (this.searchTimer !== undefined) {
            clearTimeout(this.searchTimer);
            this.searchTimer = undefined;
        }
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
    openVersions(formListItem: FormListItem): void {
        // Navigate to the versions page for the selected form template
        this.router.navigate(['/formtemplates', formListItem.formId, 'versions'], {
            queryParams:{tname: formListItem.formName}
        });
    }
}