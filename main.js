const path = require('path');
const https = require('https');
const { app, BrowserWindow, ipcMain, globalShortcut, dialog, shell } = require('electron');
const { autoUpdater } = require('electron-updater');

const isDev = !app.isPackaged;
const VERSION_MANIFEST_URL = 'https://raw.githubusercontent.com/Irish-Coder69/nys-lottery/master/version.json';
const RELEASES_URL = 'https://github.com/Irish-Coder69/nys-lottery/releases/latest';

function compareVersions(a, b) {
  const aParts = String(a).split('.').map((n) => Number.parseInt(n, 10) || 0);
  const bParts = String(b).split('.').map((n) => Number.parseInt(n, 10) || 0);
  const length = Math.max(aParts.length, bParts.length);

  for (let i = 0; i < length; i += 1) {
    const aNum = aParts[i] || 0;
    const bNum = bParts[i] || 0;
    if (aNum > bNum) return 1;
    if (aNum < bNum) return -1;
  }

  return 0;
}

function fetchJson(url) {
  return new Promise((resolve, reject) => {
    https
      .get(url, (response) => {
        if (response.statusCode !== 200) {
          reject(new Error(`HTTP ${response.statusCode}`));
          return;
        }

        let raw = '';
        response.on('data', (chunk) => {
          raw += chunk;
        });

        response.on('end', () => {
          try {
            resolve(JSON.parse(raw));
          } catch (error) {
            reject(error);
          }
        });
      })
      .on('error', reject);
  });
}

async function checkVersionManifest(mainWindow) {
  if (isDev) {
    return;
  }

  try {
    const manifest = await fetchJson(VERSION_MANIFEST_URL);
    const latestVersion = manifest && manifest.version ? manifest.version : null;
    const currentVersion = app.getVersion();

    if (!latestVersion) {
      return;
    }

    if (compareVersions(latestVersion, currentVersion) > 0) {
      const result = await dialog.showMessageBox(mainWindow, {
        type: 'info',
        buttons: ['Open Downloads', 'Later'],
        defaultId: 0,
        cancelId: 1,
        title: 'New Version Available',
        message: `Version ${latestVersion} is available. You are on ${currentVersion}.`,
        detail: manifest.notes || 'A new update is available.'
      });

      if (result.response === 0) {
        await shell.openExternal(manifest.downloadUrl || RELEASES_URL);
      }
    }
  } catch (error) {
    console.error('Version manifest check failed:', error && error.message ? error.message : error);
  }
}

function configureAutoUpdates(mainWindow) {
  if (isDev) {
    console.log('Skipping auto-update checks in development mode.');
    return;
  }

  autoUpdater.autoDownload = false;
  autoUpdater.autoInstallOnAppQuit = true;

  autoUpdater.on('checking-for-update', () => {
    console.log('Checking for updates...');
  });

  autoUpdater.on('update-available', async (info) => {
    const version = info && info.version ? info.version : 'a newer version';
    const result = await dialog.showMessageBox(mainWindow, {
      type: 'info',
      buttons: ['Download', 'Later'],
      defaultId: 0,
      cancelId: 1,
      title: 'Update Available',
      message: `Version ${version} is available. Download now?`
    });

    if (result.response === 0) {
      autoUpdater.downloadUpdate();
    }
  });

  autoUpdater.on('update-not-available', () => {
    console.log('No updates available.');
  });

  autoUpdater.on('error', (error) => {
    console.error('Auto-update error:', error == null ? 'unknown' : error.message);
  });

  autoUpdater.on('download-progress', (progressObj) => {
    console.log(`Update download progress: ${Math.round(progressObj.percent)}%`);
  });

  autoUpdater.on('update-downloaded', async () => {
    const result = await dialog.showMessageBox(mainWindow, {
      type: 'info',
      buttons: ['Restart Now', 'Later'],
      defaultId: 0,
      cancelId: 1,
      title: 'Update Ready',
      message: 'The update has been downloaded. Restart now to install it?'
    });

    if (result.response === 0) {
      autoUpdater.quitAndInstall();
    }
  });

  setTimeout(() => {
    autoUpdater.checkForUpdates();
  }, 4000);
}

//Create Main Window
function createMainWindow () {
  const mainWindow = new BrowserWindow({
    title: "NYS Lottery App",
    width:2000,
    height:1000,
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false
    }
  });

  // Maximize the window on startup
  mainWindow.maximize();

  // Don't auto-open dev tools - let F12 control it
  // mainWindow.webContents.openDevTools();

  // Add F12 handling at the main process level to ensure it works even when dev tools are open
  mainWindow.webContents.on('before-input-event', (event, input) => {
    if (input.key === 'F12') {
      event.preventDefault();
      const isDevToolsOpened = mainWindow.webContents.isDevToolsOpened();
      console.log(`🔧 F12 detected in main process. Dev tools currently open: ${isDevToolsOpened}`);
      
      if (isDevToolsOpened) {
        mainWindow.webContents.closeDevTools();
        console.log('🔒 Dev tools closed via F12');
      } else {
        mainWindow.webContents.openDevTools();
        console.log('🔓 Dev tools opened via F12');
      }
    }
  });

  mainWindow.loadFile(path.join(__dirname, './Lottery/NYS Lottery Picker.html'));
  
  return mainWindow;
}

//App is ready
app.whenReady().then(() => {
  const mainWindow = createMainWindow();
  configureAutoUpdates(mainWindow);

  setTimeout(() => {
    checkVersionManifest(mainWindow);
  }, 5000);

  setInterval(() => {
    checkVersionManifest(mainWindow);
  }, 6 * 60 * 60 * 1000);

  // Register global F12 shortcut for dev tools toggle
  globalShortcut.register('F12', () => {
    const isDevToolsOpened = mainWindow.webContents.isDevToolsOpened();
    console.log(`🌟 Global F12 shortcut triggered. Dev tools currently open: ${isDevToolsOpened}`);
    
    if (isDevToolsOpened) {
      mainWindow.webContents.closeDevTools();
      console.log('🔒 Dev tools closed via global F12');
    } else {
      mainWindow.webContents.openDevTools();
      console.log('🔓 Dev tools opened via global F12');
    }
  });

  // Handle quit message from renderer
  ipcMain.on('quit-app', () => {
    app.quit();
  });

  // Keep IPC handler as backup but make it simpler
  ipcMain.on('toggle-dev-tools', () => {
    console.log('� IPC toggle-dev-tools received');
    // Let the global shortcut or main process handler deal with it
    // This is just for debugging now
  });

  ipcMain.handle('check-for-updates', async () => {
    if (isDev) {
      return { ok: false, message: 'Update checks are only available in packaged builds.' };
    }

    try {
      await Promise.all([
        autoUpdater.checkForUpdates(),
        checkVersionManifest(mainWindow)
      ]);
      return { ok: true, message: 'Update check started.' };
    } catch (error) {
      return {
        ok: false,
        message: error && error.message ? error.message : 'Could not check for updates.'
      };
    }
  });

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createMainWindow();
    }
  });
}); 

//Quit when all windows are closed
app.on('window-all-closed', () => {
  // Unregister all global shortcuts when app is closing
  globalShortcut.unregisterAll();
  
  if (process.platform !== 'darwin') {
    app.quit();
  }
});