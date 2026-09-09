import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormControlDef } from '../../core/models/form.model';
import { FormRule } from '../../core/models/rule.model';
import { FormService } from '../../core/services/form';
import { RuleService } from '../../core/services/rule';
import { ValidationRulesTab } from '../validation-rules-tab/validation-rules-tab';
import { ConditionalRulesTab } from '../conditional-rules-tab/conditional-rules-tab';

/**
 * Shell for the Validation & Rules screen. Owns the shared data — controls and the
 * full rule list — so both tabs read from one source and a change in either refreshes
 * both, rather than each tab fetching its own copy.
 */
@Component({
  selector: 'app-form-rules',
  imports: [CommonModule, RouterLink, ValidationRulesTab, ConditionalRulesTab],
  templateUrl: './form-rules.html',
  styleUrl: './form-rules.scss',
})
export class FormRules implements OnInit {

  inumFormId!: number;
  inumVersionId!: number;
  iarrControls: FormControlDef[] = [];
  iarrRules: FormRule[] = [];

  istrActiveTab: 'validation' | 'conditional' = 'validation';

  /** Conditional rules change form state; everything else fails a submission. */
  private static readonly ConditionalTypes: string[] = ['Visibility', 'EnableDisable', 'RequiredOptional'];

  constructor(
    private iobjRoute: ActivatedRoute,
    private iobjRuleService: RuleService,
    private iobjFormService: FormService
  ) { }

  ngOnInit(): void {
    this.inumFormId = Number(this.iobjRoute.snapshot.paramMap.get('formId'));
    this.inumVersionId = Number(this.iobjRoute.snapshot.paramMap.get('versionId'));

    this.iobjFormService.getVersionById(this.inumVersionId).subscribe(res => {
      if (res.success && res.data) this.iarrControls = res.data.controls;
    });

    this.loadRules();
  }

  loadRules(): void {
    this.iobjRuleService.getRules(this.inumVersionId).subscribe(rules => this.iarrRules = rules);
  }

  get validationRules(): FormRule[] {
    return this.iarrRules.filter(r => !FormRules.ConditionalTypes.includes(r.ruleType));
  }

  get conditionalRules(): FormRule[] {
    return this.iarrRules.filter(r => FormRules.ConditionalTypes.includes(r.ruleType));
  }

  switchTab(aStrTab: 'validation' | 'conditional'): void {
    this.istrActiveTab = aStrTab;
  }
}