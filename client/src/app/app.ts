import { NgTemplateOutlet } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { ChatService } from './core/chat.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgTemplateOutlet],
  templateUrl: './app.html',
})
export class App {
  protected auth = inject(AuthService);
  protected chat = inject(ChatService); // also starts the hub connection on sign-in
  private router = inject(Router);

  protected async logout() {
    await this.auth.logout();
    await this.router.navigateByUrl('/');
  }
}
