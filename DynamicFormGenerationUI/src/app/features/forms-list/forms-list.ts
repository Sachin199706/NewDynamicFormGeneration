import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormListItem } from '../../core/models/form.model';
import { FormService } from '../../core/services/form';

@Component({
  selector: 'app-forms-list',
  imports: [RouterLink, CommonModule],
  templateUrl: './forms-list.html',
  styleUrl: './forms-list.scss',
})
export class FormsList implements OnInit {
  forms: FormListItem[] = [];

  constructor(private formService: FormService) { }

  ngOnInit(): void {
    this.formService.getForms().subscribe(res => this.forms = res.items);
  }

}
