import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateFormRuleRequest, FormRule } from '../models/rule.model';

@Injectable({
  providedIn: 'root',
})
export class RuleService {
  private istrBase = `${environment.apiUrl}/forms/versions`;

  constructor(private iobjHttp: HttpClient) { }

  getRules(aNumFormVersionId: number): Observable<FormRule[]> {
    return this.iobjHttp.get<FormRule[]>(`${this.istrBase}/${aNumFormVersionId}/rules`);
  }

  addRule(aNumFormVersionId: number, aObjDto: CreateFormRuleRequest): Observable<FormRule> {
    return this.iobjHttp.post<FormRule>(`${this.istrBase}/${aNumFormVersionId}/rules`, aObjDto);
  }

  updateRule(aNumRuleId: number, aObjDto: CreateFormRuleRequest): Observable<void> {
    return this.iobjHttp.put<void>(`${environment.apiUrl}/rules/${aNumRuleId}`, aObjDto);
  }

  deleteRule(aNumRuleId: number): Observable<void> {
    return this.iobjHttp.delete<void>(`${environment.apiUrl}/rules/${aNumRuleId}`);
  }

}