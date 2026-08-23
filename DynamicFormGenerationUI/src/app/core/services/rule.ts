import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateFormRuleRequest, FormRule } from '../models/rule.model';

@Injectable({
  providedIn: 'root',
})
export class RuleService {
  private base = `${environment.apiUrl}/forms/versions`;

  constructor(private http: HttpClient) { }

  getRules(formVersionId: number): Observable<FormRule[]> {
    return this.http.get<FormRule[]>(`${this.base}/${formVersionId}/rules`);
  }

  addRule(formVersionId: number, dto: CreateFormRuleRequest): Observable<FormRule> {
    return this.http.post<FormRule>(`${this.base}/${formVersionId}/rules`, dto);
  }

  updateRule(ruleId: number, dto: CreateFormRuleRequest): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/rules/${ruleId}`, dto);
  }

  deleteRule(ruleId: number): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/rules/${ruleId}`);
  }

}
