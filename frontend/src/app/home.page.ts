import { Component, inject } from '@angular/core';
import { AuthService } from './auth.service';
import { DoctorPage } from './doctor.page';
import { Icon } from './icon';
import { RecordsPage } from './records.page';

@Component({
  selector: 'app-home',
  imports: [Icon, RecordsPage, DoctorPage],
  templateUrl: './home.page.html',
})
export class HomePage {
  readonly auth = inject(AuthService);
}
