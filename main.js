const path = require('path');
const { app, BrowserWindow, ipcMain, globalShortcut, dialog } = require('electron');
const { autoUpdater } = require('electron-updater');

const isDev = !app.isPackaged;

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
      await autoUpdater.checkForUpdates();
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