import { defineConfig } from '@playwright/test';
import base from './playwright.config';
const server = base.webServer as any;
export default defineConfig({
  ...base, outputDir:'test-results/autoscan', reporter:[['list'],['html',{outputFolder:'playwright-report/autoscan',open:'never'}]], testMatch:/autonomous-scan\.spec\.ts/, projects:[{name:'chromium-autoscan'}],
  use:{...base.use,baseURL:'http://127.0.0.1:5204'},
  webServer:{...server,url:'http://127.0.0.1:5204/health',env:{...server.env,
    ASPNETCORE_URLS:'http://127.0.0.1:5204',
    Monitoring__StartupDelaySeconds:'1',Monitoring__PollingIntervalSeconds:'5',
    Monitoring__StatePath:'.tmp/e2e-auto/monitor.json',
    Storage__Incidents__IncidentRecordsPath:'.tmp/e2e-auto/incidents.json',
    Storage__Incidents__SessionDatabasePath:'.tmp/e2e-auto/sessions.sqlite'
  }}
});
