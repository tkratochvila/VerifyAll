import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { ColorableTextAreaComponent } from './colorable-text-area.component';

describe('ColorableTextAreaComponent', () => {
  let component: ColorableTextAreaComponent;
  let fixture: ComponentFixture<ColorableTextAreaComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ ColorableTextAreaComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ColorableTextAreaComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
