import { Component, OnInit, Input, OnChanges, SimpleChanges  } from '@angular/core';
import { Requirement } from '../requirement'

@Component({
  selector: 'app-requirement',
  templateUrl: './requirement.component.html',
  styleUrls: ['./requirement.component.less']
})
export class RequirementComponent implements OnInit {

  @Input() requirement: Requirement;

  constructor() { }

  ngOnInit(): void {
  }

}
