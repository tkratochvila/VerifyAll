import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { RequirementComponent } from './requirement.component';

describe('RequirementComponent', () => {
  let component: RequirementComponent;
  let fixture: ComponentFixture<RequirementComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ RequirementComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(RequirementComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
