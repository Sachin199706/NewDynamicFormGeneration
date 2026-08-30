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

  deleteRule(aNumFormVersionId: number, aStrControlKey: string, aStrRuleType: string): Observable<void> {
    return this.iobjHttp.delete<void>(`${this.istrBase}/${aNumFormVersionId}/rules/${aStrControlKey}/${aStrRuleType}`);
  }

}