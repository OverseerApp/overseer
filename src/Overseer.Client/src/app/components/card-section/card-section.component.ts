import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-card-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './card-section.component.html',
  host: { class: 'd-block' },
})
export class CardSectionComponent {
  @Input() icon: string = '';
  @Input() heading: string = '';
}
