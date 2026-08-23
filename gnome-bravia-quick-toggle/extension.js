import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import * as QuickSettings from 'resource:///org/gnome/shell/ui/quickSettings.js';
import {Extension} from 'resource:///org/gnome/shell/extensions/extension.js';

const _DECODER = new TextDecoder();
const _ENCODER = new TextEncoder();

// Default TV configuration – change these to match your setup.
const TV_HOST = '10.0.36.111';
const TV_PORT = 20060;
const CMD_POWER_ON = '*SCPOWR0000000000000001';
const CMD_POWER_OFF = '*SCPOWR0000000000000000';
const CMD_STATUS = '*SEPOWR################';
const CMD_HDMI1 = '*SCINPT0000000100000001';

let _indicator = null;
let _toggle = null;
let _refreshId = 0;
let _busy = false;

// Logging helpers – output lands in the GNOME Shell journal:
//   journalctl --user -b | grep -i bravia
function log(message, ...args) {
  console.log(`[bravia-quick-toggle] ${message}`, ...args);
}

function warn(message, ...args) {
  console.warn(`[bravia-quick-toggle] ${message}`, ...args);
}

function error(message, ...args) {
  console.error(`[bravia-quick-toggle] ${message}`, ...args);
}

function sendCommand(command) {
  const client = new Gio.SocketClient();
  // Bound the connection attempt so an unreachable TV can't stall the shell.
  client.timeout = 5;
  const conn = client.connect_to_host(TV_HOST, TV_PORT, null);
  try {
    // The TV answers status enquiries with a burst of notify (*SN*) frames
    // followed by the authoritative *SAPOWR answer, so keep reading until we
    // see it (or the connection goes quiet). Bound each read so a silent TV
    // can't stall the shell.
    const socket = conn.get_socket();
    if (socket)
      socket.timeout = 1;
    const out = conn.get_output_stream();
    out.write(_ENCODER.encode(command + '\n'), null);
    const dataIn = Gio.DataInputStream.new(conn.get_input_stream());
    let resp = '';
    while (true) {
      let line = null;
      try {
        [line] = dataIn.read_line(null);
      } catch (e) {
        // Read timed out: no more data coming - use what we collected.
        if (!resp)
          throw e;
        break;
      }
      if (line === null) // TV closed the connection
        break;
      resp = _DECODER.decode(line).trim();
      if (resp.startsWith('*SAPOWR')) // authoritative answer to an enquiry
        break;
    }
    if (!resp)
      throw new Error('empty response');
    return resp;
  } finally {
    conn.close(null);
  }
}

function parsePowerState(response) {
  // Prefer the *SAPOWR answer frame; fall back to the last power notify frame.
  for (const line of response.split('\n').reverse()) {
    const frame = line.trim();
    if (frame.startsWith('*SAPOWR') || frame.startsWith('*SNPOWR'))
      return frame.endsWith('0000000000000001');
  }
  return false;
}

async function refresh() {
  if (!_toggle || _busy) return;
  try {
    const resp = await new Promise((resolve, reject) => {
      try {
        resolve(sendCommand(CMD_STATUS));
      } catch (e) {
        reject(e);
      }
    });
    if (_toggle) {
      _toggle.checked = parsePowerState(resp);
      log(`status response "${resp}" -> toggle checked=${_toggle.checked}`);
    }
  } catch (e) {
    // Log network errors so we have visibility if the TV is off/sleeping at login.
    warn(`status refresh failed${_toggle ? ` (toggle checked=${_toggle.checked})` : ''}: ${e}`);
  }
}

async function togglePower() {
  if (_busy) return;
  _busy = true;

  try {
    const wantOn = _toggle.checked;
    log(`click: ${wantOn ? 'turning ON' : 'turning OFF'}`);
    await new Promise((resolve, reject) => {
      try {
        sendCommand(wantOn ? CMD_POWER_ON : CMD_POWER_OFF);
        if (wantOn) sendCommand(CMD_HDMI1);
        resolve();
      } catch (e) {
        reject(e);
      }
    });
    _toggle.checked = wantOn;
    log(`toggle set checked=${_toggle.checked} after command`);
  } catch (e) {
    error(`togglePower failed: ${e}`);
  } finally {
    _busy = false;
  }
}

export default class BraviaQuickToggle extends Extension {
  enable() {
    _busy = false;

    if (!_indicator) {
      _indicator = new QuickSettings.SystemIndicator();

      _toggle = new QuickSettings.QuickToggle({
        title: 'Bravia TV',
        iconName: 'video-display-symbolic',
        toggleMode: true,
      });
      _toggle.connectObject('clicked', () => togglePower(), this);

      _indicator.quickSettingsItems.push(_toggle);
    }

    Main.panel.statusArea.quickSettings.addExternalIndicator(_indicator);

    log('extension enabled');
    _refreshId = GLib.timeout_add_seconds(
      GLib.PRIORITY_DEFAULT,
      30,
      () => {
        refresh();
        return GLib.SOURCE_CONTINUE;
      }
    );

    refresh();
  }

  disable() {
    if (_refreshId) {
      GLib.source_remove(_refreshId);
      _refreshId = 0;
    }

    if (_indicator) {
      // Remove the indicator icon from the panel indicators box.
      const parent = _indicator.get_parent();
      if (parent) parent.remove_child(_indicator);

      // Remove the quick settings toggle from the menu grid.
      if (_toggle) {
        const qs = Main.panel.statusArea.quickSettings;
        if (_toggle.get_parent() === qs._grid)
          qs._grid.remove_child(_toggle);
        _toggle.disconnectObject(this);
        _toggle.destroy();
        _toggle = null;
      }

      _indicator.destroy();
      _indicator = null;
    }

    _busy = false;
  }
}
