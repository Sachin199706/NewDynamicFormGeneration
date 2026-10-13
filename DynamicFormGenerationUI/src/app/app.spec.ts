import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      // The sidebar uses routerLink, which needs the router.
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the sidebar with the Upload Form Image link', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    const larrTitles = Array.from(compiled.querySelectorAll('.app-sidebar__item')).map(aObjLink => aObjLink.getAttribute('title'));
    expect(larrTitles).toContain('Upload Form Image');
    expect(compiled.querySelector('a[title="Upload Form Image"]')?.getAttribute('href')).toBe('/upload-form-image');
  });
});
