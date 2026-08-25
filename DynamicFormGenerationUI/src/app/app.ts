import { Component } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs';
import { LoadingService } from './core/services/loading';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, CommonModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  iboolShowSidebar = true;

  constructor(public iobjloadingService: LoadingService, private iobjRouter: Router) {
    this.iobjRouter.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd)
    ).subscribe((e) => {
      // Public fill-in link (/forms/:id/versions/:id/fill) is the one route
      // meant to be shared outside the app — no builder chrome around it.
      this.iboolShowSidebar = !e.urlAfterRedirects.includes('/fill');
    });
  }
}