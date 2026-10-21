    /* =========================================================================
       HX STUDIO 6.1 · FRONTEND BRIDGE, STATE STORE & RENDER ENGINE
       Strictly compliant with UI_SPEC_FOR_FRONTEND_AI.md
       ========================================================================= */
    (function () {
      'use strict';

      const bootTimestamp = performance.now();
      const hasHost = !!(window.chrome && window.chrome.webview);
      let usingMock = !hasHost;
      let receivedAnyEvt = false;
      let cmdSeq = 0;
      const pendingCmds = new Map();

      // Animation lifecycle flags (Section 7 requirement)
      let isPageHidden = document.visibilityState === 'hidden';
      let isWindowMinimized = false;
      let isPerfLow = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      if (isPerfLow) {
        document.body.classList.add('perf-low');
      }

      // Application Reactive State (Zero hardcoded hardware values)
      const state = {
        currentPage: 'device',
        theme: 'light',
        safetyCoverOpen: false,
        selectedUnlockCard: '90HX',
        selectedUnlockChannel: 'A',
        selectedSpoofSlot: 0,
        selectedSpoofTarget: '',
        logsFilterLevel: 'all',
        logsSearchKeyword: '',
        logsAutoScroll: true,
        logsList: [],
        pendingConfirm: null,

        // Section 5 Data Contracts + 3-state tracking ('loading' | 'ready' | 'error')
        deviceStatus: 'loading',
        deviceError: null,
        deviceSnapshot: null,
        gpuList: [],

        unlockStatus: 'loading',
        unlockError: null,
        unlockState: null,

        spoofStatus: 'loading',
        spoofError: null,
        spoofModels: null,
        spoofState: null,

        powerStatus: 'loading',
        powerError: null,
        powerState: null,

        dlssStatus: 'loading',
        dlssError: null,
        dlssState: null,
        dlssPreflight: null,

        overlayStatus: 'loading',
        overlayError: null,
        overlayState: null,

        dualStatus: 'loading',
        dualError: null,
        dualState: null,

        maintenanceStatus: 'loading',
        maintenanceError: null,
        snapshots: null,

        logsStatus: 'ready',
        logsError: null,

        updateStatus: 'loading',
        updateError: null,
        updateState: null
      };

      const ERROR_CODE_LABELS = {
        NO_ADMIN: '缺少管理员权限',
        GPU_NOT_FOUND: '未检测到目标显卡',
        DRIVER_REJECT: '驱动程序拒绝该操作',
        FILE_LOCKED: '目标文件被占用或锁定',
        NETWORK: '网络连接失败或镜像不可达',
        CHECKSUM_MISMATCH: 'SHA-256 校验和不匹配',
        NEED_REBOOT: '操作已提交，需重启系统生效',
        NOT_SUPPORTED: '当前硬件或系统不支持该功能',
        INTERNAL: '宿主内部执行异常'
      };

      function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str)
          .replace(/&/g, '&amp;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;')
          .replace(/"/g, '&quot;')
          .replace(/'/g, '&#39;');
      }

      function valOrUnknown(val, suffix) {
        if (val === undefined || val === null || val === '') return '未知';
        return String(val) + (suffix || '');
      }

      /* 3D 模型上的字位很窄：伪装后型号名可能变成
         "NVIDIA GeForce RTX 3080" 这种长串，必须压缩后再放上去 */
      function shortModelName(raw) {
        let s = String(raw === undefined || raw === null ? '' : raw).trim();
        if (!s) return 'CMP';
        s = s.replace(/^NVIDIA\s+/i, '')
             .replace(/^GeForce\s+/i, '')
             .replace(/^Tesla\s+/i, '')
             .replace(/^Quadro\s+/i, '');
        if (s.length > 15) s = s.slice(0, 14) + '…';
        return s;
      }

      /* 中间风扇只放数字：'CMP 90HX' → '90'，'RTX 3080' → '3080' */
      function digitsOnly(raw) {
        const m = String(raw === undefined || raw === null ? '' : raw).match(/\d+/);
        return m ? m[0] : '--';
      }

      function showToast(text, level) {
        const stack = document.getElementById('toastStack');
        if (!stack) return;
        const item = document.createElement('div');
        item.className = 'toast-item';
        let dotVar = 'var(--accent)';
        if (level === 'error' || level === 'danger') dotVar = 'var(--danger)';
        else if (level === 'warn' || level === 'warning') dotVar = 'var(--warning)';
        else if (level === 'success') dotVar = 'var(--success)';

        const dot = document.createElement('span');
        dot.style.cssText = 'width:8px;height:8px;border-radius:50%;flex-shrink:0;background:' + dotVar + ';';
        const span = document.createElement('span');
        span.textContent = text;

        item.appendChild(dot);
        item.appendChild(span);
        stack.appendChild(item);

        setTimeout(() => {
          item.style.opacity = '0';
          item.style.transition = 'opacity 180ms cubic-bezier(0.2, 0.8, 0.2, 1)';
          setTimeout(() => item.remove(), 180);
        }, 3000);
      }

      function renderStateBanner(containerId, status, errorObj, retryAction, emptyText, emptyBtnLabel, emptyBtnAction) {
        const el = document.getElementById(containerId);
        if (!el) return;
        if (status === 'loading') {
          el.innerHTML = '<div class="state-box"><div class="state-box-text"><span>正在等待宿主同步数据...</span><div class="skeleton-line" style="width:216px;margin-top:4px;"></div></div></div>';
        } else if (status === 'error' && errorObj) {
          const code = escapeHtml(errorObj.code || 'INTERNAL');
          const desc = escapeHtml(errorObj.message || ERROR_CODE_LABELS[errorObj.code] || '未知错误');
          el.innerHTML = '<div class="state-box error"><div class="state-box-text"><div><span class="error-code-pill">' + code + '</span><strong>' + desc + '</strong></div></div>' +
            (retryAction ? '<button class="btn btn-secondary" data-action="' + escapeHtml(retryAction) + '">重试</button>' : '') +
            '</div>';
        } else if (status === 'empty') {
          el.innerHTML = '<div class="state-box"><div class="state-box-text">' + escapeHtml(emptyText || '当前暂无数据') + '</div>' +
            (emptyBtnAction ? '<button class="btn btn-primary" data-action="' + escapeHtml(emptyBtnAction) + '">' + escapeHtml(emptyBtnLabel || '立即执行') + '</button>' : '') +
            '</div>';
        } else {
          el.innerHTML = '';
        }
      }

      /* =======================================================================
         SECTION 4: UNIFIED BRIDGE (cmd / res / evt / prg)
         ======================================================================= */
      function nextCmdId() {
        cmdSeq += 1;
        return 'c-' + String(cmdSeq).padStart(4, '0');
      }

      function sendCmd(action, payload, triggerBtn) {
        const id = nextCmdId();
        const envelope = {
          kind: 'cmd',
          id: id,
          action: action,
          payload: payload || {}
        };

        if (triggerBtn && triggerBtn.classList) {
          triggerBtn.classList.add('is-loading');
          triggerBtn.disabled = true;
        }

        return new Promise((resolve) => {
          pendingCmds.set(id, { action, triggerBtn, resolve });
          if (hasHost && !usingMock) {
            try {
              window.chrome.webview.postMessage(envelope);
            } catch (err) {
              handleIncomingEnvelope({
                kind: 'res',
                id: id,
                action: action,
                payload: { ok: false, data: null, error: { code: 'INTERNAL', message: String(err) } }
              });
            }
          } else {
            dispatchToMockHost(envelope);
          }
        });
      }

      function handleIncomingEnvelope(raw) {
        if (!raw) return;
        let msg = raw;
        if (typeof raw === 'string') {
          try {
            msg = JSON.parse(raw);
          } catch (err) {
            // 单条消息格式错误不能把整条通道弄断
            console.error('[HX] 无法解析宿主消息:', err, String(raw).slice(0, 200));
            return;
          }
        }
        if (!msg || !msg.kind) return;

        if (msg.kind === 'evt') {
          receivedAnyEvt = true;
          handleEvent(msg.action, msg.payload || {});
        } else if (msg.kind === 'prg') {
          handleProgress(msg.id, msg.action, msg.payload || {});
        } else if (msg.kind === 'res') {
          const pending = pendingCmds.get(msg.id);
          if (pending) {
            pendingCmds.delete(msg.id);
            if (pending.triggerBtn && pending.triggerBtn.classList) {
              pending.triggerBtn.classList.remove('is-loading');
              pending.triggerBtn.disabled = false;
            }
            handleCommandResponse(pending.action, msg.payload || {}, pending.resolve);
          } else {
            handleCommandResponse(msg.action, msg.payload || {}, null);
          }
        }
      }

      function handleCommandResponse(action, resPayload, resolve) {
        const ok = !!resPayload.ok;
        const err = resPayload.error;
        if (!ok && err) {
          const code = err.code || 'INTERNAL';
          const cn = ERROR_CODE_LABELS[code] || '';
          const fullMsg = '[' + code + '] ' + (err.message || cn || '操作失败');
          showToast(fullMsg, 'error');

          if (action.startsWith('device.')) { state.deviceStatus = 'error'; state.deviceError = err; renderDevicePage(); }
          else if (action.startsWith('unlock.')) { state.unlockStatus = 'error'; state.unlockError = err; renderUnlockPage(); }
          else if (action.startsWith('spoof.')) { state.spoofStatus = 'error'; state.spoofError = err; renderSpoofPage(); }
          else if (action.startsWith('power.')) { state.powerStatus = 'error'; state.powerError = err; renderPowerPage(); }
          else if (action.startsWith('dlss.')) { state.dlssStatus = 'error'; state.dlssError = err; renderDlssPage(); }
          else if (action.startsWith('overlay.')) { state.overlayStatus = 'error'; state.overlayError = err; renderOverlayPage(); }
          else if (action.startsWith('dual.')) { state.dualStatus = 'error'; state.dualError = err; renderDualPage(); }
          else if (action.startsWith('snapshot.') || action.startsWith('factory.')) { state.maintenanceStatus = 'error'; state.maintenanceError = err; renderMaintenancePage(); }
          else if (action.startsWith('logs.')) { state.logsStatus = 'error'; state.logsError = err; renderLogsPage(); }
          else if (action.startsWith('update.')) { state.updateStatus = 'error'; state.updateError = err; renderUpdatePage(); }
        } else if (ok) {
          const data = resPayload.data || {};
          if (action === 'dialog.browse' && data.path && state.lastBrowseTargetId) {
            const inputEl = document.getElementById(state.lastBrowseTargetId);
            if (inputEl) inputEl.value = data.path;
            if (state.lastBrowseTargetId === 'dlssGameDirInput') {
              sendCmd('dlss.preflight', getDlssPreflightPayload());
            }
          } else if (action === 'logs.query' && Array.isArray(data.items)) {
            state.logsList = data.items.concat(state.logsList);
            state.logsStatus = 'ready';
            renderLogsPage();
          } else if (action === 'logs.clear') {
            state.logsList = [];
            renderLogsPage();
            showToast('日志已清空', 'success');
          } else if (action !== 'app.init' && action !== 'app.ready' && !action.startsWith('window.') && !action.startsWith('theme.')) {
            if (data.message) {
              showToast(data.message, 'success');
            }
          }
        }
        if (resolve) resolve(resPayload);
      }

      function handleProgress(id, action, prg) {
        const percent = typeof prg.percent === 'number' ? prg.percent : 0;
        const stage = prg.stage || '执行中';
        const detail = prg.detail ? ' (' + prg.detail + ')' : '';

        if (action && action.startsWith('update.')) {
          const wrap = document.getElementById('updateProgressWrap');
          const stageEl = document.getElementById('updateProgressStage');
          const pctEl = document.getElementById('updateProgressPercent');
          const fillEl = document.getElementById('updateProgressFill');
          if (wrap) wrap.classList.add('active');
          if (stageEl) stageEl.textContent = stage + detail;
          if (fillEl) {
            if (percent === -1) {
              fillEl.classList.add('indeterminate');
              if (pctEl) pctEl.textContent = '进行中';
            } else {
              fillEl.classList.remove('indeterminate');
              fillEl.style.width = Math.max(0, Math.min(100, percent)) + '%';
              if (pctEl) pctEl.textContent = Math.round(percent) + '%';
            }
          }
        } else {
          const wrap = document.getElementById('dlssProgressWrap');
          const stageEl = document.getElementById('dlssProgressStage');
          const pctEl = document.getElementById('dlssProgressPercent');
          const fillEl = document.getElementById('dlssProgressFill');
          if (wrap) wrap.classList.add('active');
          if (stageEl) stageEl.textContent = stage + detail;
          if (fillEl) {
            if (percent === -1) {
              fillEl.classList.add('indeterminate');
              if (pctEl) pctEl.textContent = '进行中';
            } else {
              fillEl.classList.remove('indeterminate');
              fillEl.style.width = Math.max(0, Math.min(100, percent)) + '%';
              if (pctEl) pctEl.textContent = Math.round(percent) + '%';
            }
          }
        }
      }

      function handleEvent(action, payload) {
        switch (action) {
          case 'device.snapshot':
            state.deviceSnapshot = payload;
            state.deviceStatus = (payload && payload.gpu && payload.gpu.name) ? 'ready' : 'empty';
            state.deviceError = null;
            renderDevicePage();
            break;
          case 'sensors.tick':
            if (state.deviceSnapshot && state.deviceSnapshot.sensors) {
              Object.assign(state.deviceSnapshot.sensors, payload);
              state.deviceSnapshot.sensors.valid = true;
              renderSensorsSection();
            }
            break;
          case 'gpu.list':
            state.gpuList = Array.isArray(payload.gpus) ? payload.gpus : [];
            renderGpuListSection();
            break;
          case 'unlock.state':
            state.unlockState = payload;
            state.unlockStatus = 'ready';
            state.unlockError = null;
            renderUnlockPage();
            break;
          case 'spoof.models':
            state.spoofModels = payload;
            if (payload && payload.recommended && !state.selectedSpoofTarget) {
              state.selectedSpoofTarget = payload.recommended;
            }
            renderSpoofModelsDropdown();
            renderSpoofPage();
            break;
          case 'spoof.state':
            state.spoofState = payload;
            state.spoofStatus = (payload && Array.isArray(payload.gpus) && payload.gpus.length > 0) ? 'ready' : 'empty';
            state.spoofError = null;
            renderSpoofPage();
            break;
          case 'power.state':
            state.powerState = payload;
            state.powerStatus = 'ready';
            state.powerError = null;
            renderPowerPage();
            break;
          case 'dlss.state':
            state.dlssState = payload;
            state.dlssStatus = 'ready';
            state.dlssError = null;
            renderDlssPage();
            break;
          case 'dlss.preflight':
            state.dlssPreflight = payload;
            renderDlssPreflightSection();
            break;
          case 'overlay.state':
            state.overlayState = payload;
            state.overlayStatus = 'ready';
            state.overlayError = null;
            renderOverlayPage();
            break;
          case 'dual.state':
            state.dualState = payload;
            state.dualStatus = 'ready';
            state.dualError = null;
            renderDualPage();
            break;
          case 'maintenance.snapshots':
            state.snapshots = Array.isArray(payload.items) ? payload.items : [];
            state.maintenanceStatus = state.snapshots.length > 0 ? 'ready' : 'empty';
            state.maintenanceError = null;
            renderMaintenancePage();
            break;
          case 'logs.append':
            if (payload && payload.text) {
              state.logsList.push(payload);
              if (state.logsList.length > 500) state.logsList.shift();
              renderLogsPage(true);
            }
            break;
          case 'app.version': {
            // 版本号由宿主推送，不在页面里写死
            const verEl0 = document.getElementById('titlebarVersionBadge');
            if (verEl0 && payload && payload.version) verEl0.textContent = 'v' + payload.version;
            break;
          }
          case 'update.state':
            state.updateState = payload;
            state.updateStatus = 'ready';
            state.updateError = null;
            if (payload && payload.currentVersion) {
              const verEl = document.getElementById('titlebarVersionBadge');
              if (verEl) verEl.textContent = 'v' + payload.currentVersion;
            }
            renderUpdatePage();
            break;
          case 'app.toast':
            showToast(payload.text || '', payload.level || 'info');
            break;
          case 'app.busy': {
            const bar = document.getElementById('globalBusyBar');
            const txt = document.getElementById('globalBusyText');
            if (bar) bar.classList.toggle('active', !!payload.busy);
            if (txt && payload.text) txt.textContent = payload.text;
            break;
          }
          case 'theme.apply':
            applyThemeVisual(payload.theme === 'light' ? 'light' : 'dark');
            break;
        }
      }

      /* =======================================================================
         PAGE RENDERERS (10 PAGES, 3-STATE COMPLIANT, HIGH VISUAL DENSITY)
         ======================================================================= */

      // 1. DEVICE PAGE RENDERER
      function renderDevicePage() {
        renderStateBanner(
          'deviceStateBanner',
          state.deviceStatus,
          state.deviceError,
          'device.refresh',
          '未检测到受支持的显卡设备，请检查物理插槽或重新扫描。',
          '重新扫描硬件',
          'device.refresh'
        );

        const snap = state.deviceSnapshot;
        const gpu = snap && snap.gpu ? snap.gpu : null;

        // Sidebar footer update
        const sideModel = document.getElementById('sidebarGpuModel');
        const sideBus = document.getElementById('sidebarBusText');
        const sideDot = document.getElementById('sidebarBusDot');
        if (sideModel) sideModel.textContent = gpu && gpu.model ? gpu.model : '未知';
        if (sideBus) sideBus.textContent = gpu && gpu.bus ? gpu.bus : '未知';
        if (sideDot) sideDot.style.background = (gpu && gpu.name) ? 'var(--success)' : 'var(--text-muted)';

        // Hero Banner
        const nameEl = document.getElementById('heroGpuDisplayName');
        const badgeEl = document.getElementById('heroSpoofBadge');
        const tagsEl = document.getElementById('heroGpuTags');
        if (nameEl) nameEl.textContent = gpu && (gpu.displayName || gpu.name) ? (gpu.displayName || gpu.name) : '未知显卡设备';
        if (badgeEl) {
          if (!gpu || !gpu.name) {
            badgeEl.className = 'tag';
            badgeEl.textContent = '未知状态';
          } else if (gpu.isSpoofed) {
            badgeEl.className = 'tag success';
            badgeEl.textContent = '伪装生效 (' + valOrUnknown(gpu.model) + ')';
          } else {
            badgeEl.className = 'tag accent';
            badgeEl.textContent = '原厂标识';
          }
        }
        if (tagsEl) {
          tagsEl.innerHTML =
            '<span class="tag" title="显存容量与位宽类型">' + escapeHtml(valOrUnknown(gpu && gpu.vram)) + '</span>' +
            '<span class="tag accent" title="当前显卡驱动版本">驱动 ' + escapeHtml(valOrUnknown(gpu && gpu.driverVersion)) + '</span>';
        }

        // 3D Card Labels
        // 三个风扇分工：左=品牌标记(RX/HX)，中=纯数字型号，右=芯片代号
        const bBrand = document.getElementById('gpu3dBadgeBrand');
        const bModel = document.getElementById('gpu3dBadgeModel');
        const bChip = document.getElementById('gpu3dBadgeChip');
        const bBack = document.getElementById('gpu3dBackText');
        const bLight = document.getElementById('gpu3dLightbarText');
        if (bBrand) bBrand.textContent = (gpu && gpu.isSpoofed) ? 'RX' : 'HX';
        if (bModel) bModel.textContent = digitsOnly(gpu && gpu.model);
        if (bChip) bChip.textContent = gpu && gpu.chip ? gpu.chip : '--';
        if (bBack) bBack.textContent = gpu && gpu.chip ? gpu.chip : 'CMP';
        if (bLight) bLight.textContent = shortModelName(gpu && gpu.model);

        // PCIe Lanes
        const pcieTxt = document.getElementById('pcieFormattedText');
        const lanesGrid = document.getElementById('pcieLanesGrid');
        if (pcieTxt) {
          pcieTxt.textContent = gpu && gpu.pcieFormatted
            ? ('PCIe 链路: ' + gpu.pcieFormatted)
            : 'PCIe 链路状态: 未知';
        }
        if (lanesGrid) {
          const activeWidth = gpu && gpu.pcieWidth ? parseInt(gpu.pcieWidth, 10) || 0 : 0;
          const pins = lanesGrid.querySelectorAll('.lane-pin');
          pins.forEach((pin, idx) => {
            pin.classList.toggle('inactive', idx >= activeWidth);
          });
        }

        renderSensorsSection();
        renderSystemMatrixSection();
        renderGpuListSection();
      }

      function renderSensorsSection() {
        const snap = state.deviceSnapshot;
        const s = snap && snap.sensors ? snap.sensors : null;
        const valid = !!(s && s.valid);

        const cards = ['gaugeCardTemp', 'gaugeCardFan', 'gaugeCardPower', 'gaugeCardMem'];
        cards.forEach(id => {
          const el = document.getElementById(id);
          if (el) el.classList.toggle('sensor-invalid', !valid);
        });

        const ringTemp = document.getElementById('ringGpuTemp');
        const txtTemp = document.getElementById('textGpuTemp');
        const subTemp = document.getElementById('subGpuTemp');

        const ringFan = document.getElementById('ringFanPercent');
        const txtFan = document.getElementById('textFanPercent');
        const subFan = document.getElementById('subFanUtil');

        const ringPwr = document.getElementById('ringBoardPower');
        const txtPwr = document.getElementById('textBoardPower');
        const subPwr = document.getElementById('subBoardPower');

        const ringMem = document.getElementById('ringMemUsed');
        const txtMem = document.getElementById('textMemUsed');
        const subMem = document.getElementById('subMemClock');

        if (!valid) {
          if (ringTemp) ringTemp.style.strokeDashoffset = '60';
          if (txtTemp) txtTemp.textContent = '不可用';
          if (subTemp) subTemp.innerHTML = '<span class="gs-k">降频阈值</span><span class="gs-v">--</span><span class="gs-k">关断阈值</span><span class="gs-v">--</span>';

          if (ringFan) ringFan.style.strokeDashoffset = '60';
          if (txtFan) txtFan.textContent = '不可用';
          if (subFan) subFan.innerHTML = '<span class="gs-k">核心负载</span><span class="gs-v">--</span><span class="gs-k">性能状态</span><span class="gs-v">--</span>';

          if (ringPwr) ringPwr.style.strokeDashoffset = '60';
          if (txtPwr) txtPwr.textContent = '不可用';
          if (subPwr) subPwr.innerHTML = '<span class="gs-k">功耗上限</span><span class="gs-v">--</span><span class="gs-k">默认上限</span><span class="gs-v">--</span>';

          if (ringMem) ringMem.style.strokeDashoffset = '60';
          if (txtMem) txtMem.textContent = '不可用';
          if (subMem) subMem.innerHTML = '<span class="gs-k">核心频率</span><span class="gs-v">--</span><span class="gs-k">显存频率</span><span class="gs-v">--</span>';
          return;
        }

        const flashVal = (el) => {
          if (!el) return;
          el.classList.remove('val-flash');
          void el.offsetWidth;
          el.classList.add('val-flash');
        };

        const tempRatio = Math.max(0, Math.min(1, (s.temp || 0) / (s.tempShutdownC || 100)));
        if (ringTemp) ringTemp.style.strokeDashoffset = String(60 * (1 - tempRatio));
        const barTemp = document.getElementById('barGpuTemp'); if (barTemp) barTemp.style.width = (tempRatio * 100).toFixed(1) + '%';
        if (txtTemp) { txtTemp.innerHTML = escapeHtml(s.temp) + '<span class="gauge-unit">°C</span>'; flashVal(txtTemp); }
        const tempSubStr = '降频 ' + valOrUnknown(s.tempSlowdownC, '°C') + ' / 关断 ' + valOrUnknown(s.tempShutdownC, '°C');
        if (subTemp) {
          subTemp.innerHTML =
            '<span class="gs-pair">高温阈值 <strong>' + escapeHtml(valOrUnknown(s.tempSlowdownC, '°C')) + '</strong></span>' +
            '<span class="gs-pair">保护 <strong>' + escapeHtml(valOrUnknown(s.tempShutdownC, '°C')) + '</strong></span>';
        }
        // 明细已由卡片下方的 .gauge-sub 展示，这里不再给整卡挂 title：
        // 原生 title 悬停时会弹出系统样式的小方框，和界面风格不符

        const fanRatio = Math.max(0, Math.min(1, (s.fanPercent || 0) / 100));
        if (ringFan) ringFan.style.strokeDashoffset = String(60 * (1 - fanRatio));
        const barFan = document.getElementById('barFanPercent'); if (barFan) barFan.style.width = (fanRatio * 100).toFixed(1) + '%';
        if (txtFan) { txtFan.innerHTML = escapeHtml(s.fanPercent) + '<span class="gauge-unit">%</span>'; flashVal(txtFan); }
        const fanSubStr = '负载 ' + valOrUnknown(s.utilPercent, '%') + ' · ' + valOrUnknown(s.pstate);
        if (subFan) {
          subFan.innerHTML =
            '<span class="gs-pair">核心负载 <strong>' + escapeHtml(valOrUnknown(s.utilPercent, '%')) + '</strong></span>' +
            '<span class="gs-pair">状态 <strong>' + escapeHtml(valOrUnknown(s.pstate)) + '</strong></span>';
        }
        // 同上：风扇卡不挂 title，避免系统小方框

        const pwrLimit = s.powerLimit || s.powerDefaultLimit || 300;
        const pwrRatio = Math.max(0, Math.min(1, (s.powerDraw || 0) / pwrLimit));
        if (ringPwr) ringPwr.style.strokeDashoffset = String(60 * (1 - pwrRatio));
        const barPwr = document.getElementById('barBoardPower'); if (barPwr) barPwr.style.width = (pwrRatio * 100).toFixed(1) + '%';
        if (txtPwr) { txtPwr.innerHTML = escapeHtml(Number(s.powerDraw || 0).toFixed(1)) + '<span class="gauge-unit">W</span>'; flashVal(txtPwr); }
        const pwrSubStr = '上限 ' + valOrUnknown(s.powerLimit, 'W') + ' / 默认 ' + valOrUnknown(s.powerDefaultLimit, 'W');
        if (subPwr) {
          subPwr.innerHTML =
            '<span class="gs-pair">功耗上限 <strong>' + escapeHtml(valOrUnknown(s.powerLimit, 'W')) + '</strong></span>' +
            '<span class="gs-pair">默认 <strong>' + escapeHtml(valOrUnknown(s.powerDefaultLimit, 'W')) + '</strong></span>';
        }
        // 同上：功耗卡不挂 title

        const memTotal = s.memTotalMB || 1;
        const memRatio = Math.max(0, Math.min(1, (s.memUsedMB || 0) / memTotal));
        if (ringMem) ringMem.style.strokeDashoffset = String(60 * (1 - memRatio));
        const barMem = document.getElementById('barMemUsed'); if (barMem) barMem.style.width = (memRatio * 100).toFixed(1) + '%';
        if (txtMem) { txtMem.innerHTML = escapeHtml(s.memUsedMB) + '<span class="gauge-unit">MB</span>'; flashVal(txtMem); }
        const memSubStr = '核心 ' + valOrUnknown(s.coreClock, 'MHz') + ' · 显存 ' + valOrUnknown(s.memClock, 'MHz');
        if (subMem) {
          subMem.innerHTML =
            '<span class="gs-pair">核心 <strong>' + escapeHtml(valOrUnknown(s.coreClock, 'MHz')) + '</strong></span>' +
            '<span class="gs-pair">显存 <strong>' + escapeHtml(valOrUnknown(s.memClock, 'MHz')) + '</strong></span>';
        }
        // 同上：显存卡不挂 title

        const pVal = document.getElementById('powerReactorValText');
        const pSub = document.getElementById('powerReactorSubText');
        if (pVal) pVal.textContent = Number(s.powerDraw || 0).toFixed(1) + ' W';
        if (pSub) pSub.textContent = (s.coreClock || '--') + ' MHz';
      }

      function renderSystemMatrixSection() {
        const snap = state.deviceSnapshot;
        const sys = snap && snap.system ? snap.system : null;
        const flags = snap && snap.flags ? snap.flags : null;
        const matrixEl = document.getElementById('deviceSystemMatrix');
        const flagBadge = document.getElementById('deviceFlagsSummary');

        if (flagBadge) {
          if (!flags) {
            flagBadge.className = 'tag';
            flagBadge.textContent = '权限未知';
          } else {
            flagBadge.className = flags.isAdmin ? 'tag success' : 'tag danger';
            flagBadge.textContent = (flags.isAdmin ? '管理员权限' : '无管理员权限') +
              ' · 省电守护' + (flags.isPowerSaverRunning ? '运行中' : '已关闭');
          }
        }

        if (!matrixEl) return;
        if (!sys) {
          matrixEl.innerHTML = '<div class="state-box" style="grid-column:1/-1;"><div class="state-box-text">系统信息未知</div></div>';
          return;
        }
        const gpu = snap && snap.gpu ? snap.gpu : {};

        const cells = [
          { title: '核心与设备标识', sub: valOrUnknown(gpu.chip) + ' · ' + valOrUnknown(gpu.deviceId) + ' · 总线 ' + valOrUnknown(gpu.bus), tone: 'pass' },
          { title: '处理器', sub: valOrUnknown(sys.cpuName) + ' · ' + valOrUnknown(sys.cpuCores), tone: 'pass' },
          { title: '主板与内存', sub: valOrUnknown(sys.motherboard) + ' · ' + valOrUnknown(sys.memory), tone: 'pass' },
          { title: '操作系统', sub: valOrUnknown(sys.os) + ' · 运行 ' + valOrUnknown(sys.uptime), tone: 'pass' },
          { title: 'UEFI 引导', sub: (sys.uefi ? 'UEFI 已开启' : '传统引导') + ' · 安全启动' + (sys.secureBoot ? '开启' : '已关闭'), tone: sys.secureBoot ? 'warn' : 'pass' },
          { title: 'HAGS 硬件调度', sub: sys.hags ? '已开启' : '未开启', tone: sys.hags ? 'pass' : 'warn' }
        ];

        matrixEl.innerHTML = cells.map(c =>
          '<div class="matrix-item">' +
            '<div class="matrix-icon-box ' + c.tone + '">' +
              '<svg class="svg-icon" viewBox="0 0 24 24"><circle class="svg-tone" cx="12" cy="12" r="8.5"/><path d="M5.5 12.5l4.5 4.5L19 7.5"/></svg>' +
            '</div>' +
            '<div>' +
              '<div class="matrix-title">' + escapeHtml(c.title) + '</div>' +
              '<div class="matrix-sub">' + escapeHtml(c.sub) + '</div>' +
            '</div>' +
          '</div>'
        ).join('');
      }

      function renderGpuListSection() {
        const container = document.getElementById('gpuListContainer');
        const badge = document.getElementById('gpuListCountBadge');
        const list = state.gpuList || [];
        if (badge) badge.textContent = list.length + ' 张卡';
        if (!container) return;

        if (list.length === 0) {
          container.innerHTML = '<div class="state-box"><div class="state-box-text">未检测到显卡槽位数据</div><button class="btn btn-secondary" data-action="device.refresh">重新读取</button></div>';
          return;
        }

        container.innerHTML = '<table class="data-table"><thead><tr>' +
          '<th title="slot">槽位</th><th title="detectedModel">物理型号</th><th>核心 / 显存</th><th title="currentDesc">当前型号</th><th title="targetName">目标伪装</th><th>状态</th>' +
          '</tr></thead><tbody>' +
          list.map(g =>
            '<tr>' +
              '<td>#' + escapeHtml(g.slot) + '</td>' +
              '<td>' + escapeHtml(valOrUnknown(g.detectedModel)) + '</td>' +
              '<td>' + escapeHtml(valOrUnknown(g.chip)) + ' / ' + escapeHtml(valOrUnknown(g.vram)) + '</td>' +
              '<td>' + escapeHtml(valOrUnknown(g.currentDesc)) + '</td>' +
              '<td>' + escapeHtml(valOrUnknown(g.targetName)) + '</td>' +
              '<td>' +
                '<span class="tag ' + (g.isSpoofed ? 'success' : '') + '">伪装:' + (g.isSpoofed ? '是' : '否') + '</span> ' +
                '<span class="tag ' + (g.isPcieUnlocked ? 'accent' : '') + '">PCIe解锁:' + (g.isPcieUnlocked ? '是' : '否') + '</span> ' +
                '<span class="tag ' + (g.isComputeUnlocked ? 'accent' : '') + '">计算解锁:' + (g.isComputeUnlocked ? '是' : '否') + '</span>' +
              '</td>' +
            '</tr>'
          ).join('') +
          '</tbody></table>';
      }

      // 2. UNLOCK PAGE RENDERER (3 CARD TYPES x 4 CHANNELS)
      function renderUnlockPage() {
        renderStateBanner('unlockStateBanner', state.unlockStatus, state.unlockError, 'unlock.scan');

        document.querySelectorAll('#unlockCardTypeBar .seg-btn').forEach(btn => {
          btn.classList.toggle('active', btn.dataset.card === state.selectedUnlockCard);
        });

        const u = state.unlockState;
        const modeTag = document.getElementById('unlockModeTag');
        const envFlags = document.getElementById('unlockEnvFlags');
        const detailTag = document.getElementById('unlockDetailTag');
        const channelsGrid = document.getElementById('unlockChannelsGrid');
        const usbRow = document.getElementById('unlockUsbRow');
        const usbSelect = document.getElementById('unlockUsbSelect');

        if (!u) return;

        if (u.mode === 'DirtyState') {
          const banner = document.getElementById('unlockStateBanner');
          if (banner) {
            banner.innerHTML = '<div class="state-box error"><div class="state-box-text"><strong>检测到引导异常中断：</strong><span>' +
              escapeHtml(u.detail || '上次引导注入未完整结束，请立即执行恢复官方引导或重新注入以修复引导链。') +
              '</span></div><button class="btn btn-danger" data-action="modal.confirm" data-confirm-type="unlock.rollback">立即修复/回滚</button></div>';
          }
        }

        if (modeTag) {
          modeTag.className = 'tag ' + (u.mode === 'DirtyState' ? 'danger' : (u.mode === 'None' ? '' : 'success'));
          modeTag.textContent = valOrUnknown(u.modeLabel || u.mode);
        }

        if (envFlags) {
          envFlags.innerHTML =
            '<span class="tag ' + (u.isUefi ? 'success' : 'warning') + '">UEFI: ' + (u.isUefi ? '是' : '否') + '</span>' +
            '<span class="tag ' + (!u.secureBoot ? 'success' : 'danger') + '">安全启动: ' + (u.secureBoot ? '开启' : '关闭') + '</span>' +
            '<span class="tag ' + (u.espMounted ? 'accent' : '') + '">ESP挂载: ' + (u.espMounted ? '是' : '否') + '</span>' +
            '<span class="tag ' + (u.hasBackup ? 'success' : 'warning') + '">引导备份: ' + (u.hasBackup ? '完好' : '无') + '</span>';
        }

        if (detailTag) {
          detailTag.textContent = valOrUnknown(u.detail);
        }

        const channels = Array.isArray(u.channels) && u.channels.length > 0 ? u.channels : [
          { id: 'A', label: 'UEFI 直启', available: true, installed: false },
          { id: 'B', label: 'UEFI Shell 链启', available: true, installed: false },
          { id: 'C', label: 'U 盘引导', available: true, installed: false },
          { id: 'D', label: '恢复官方引导', available: true, installed: false }
        ];

        if (channelsGrid) {
          channelsGrid.innerHTML = channels.map(ch => {
            const isSel = state.selectedUnlockChannel === ch.id;
            const statusPill = ch.installed
              ? '<span class="tag success">当前生效</span>'
              : (ch.available ? '<span class="tag">可用</span>' : '<span class="tag danger">不可用</span>');
            return '<div class="channel-card ' + (isSel ? 'active ' : '') + (!ch.available ? 'disabled' : '') + '" ' +
              'data-action="unlock.selectChannel" data-channel="' + escapeHtml(ch.id) + '" tabindex="0" role="button">' +
              '<div style="display:flex;justify-content:space-between;align-items:center;">' +
                '<strong style="font-size:12.5px;color:var(--text-main);">通道 ' + escapeHtml(ch.id) + '</strong>' +
                statusPill +
              '</div>' +
              '<div style="font-size:12.5px;font-weight:700;color:var(--accent);">' + escapeHtml(ch.label) + '</div>' +
            '</div>';
          }).join('');
        }

        const needsUsb = state.selectedUnlockChannel === 'B' || state.selectedUnlockChannel === 'C';
        if (usbRow) {
          usbRow.style.display = needsUsb ? 'flex' : 'none';
        }
        if (usbSelect && needsUsb) {
          const drives = Array.isArray(u.usbDrives) ? u.usbDrives : [];
          if (drives.length === 0) {
            usbSelect.innerHTML = '<option value="">未检测到可用 U 盘</option>';
          } else {
            usbSelect.innerHTML = drives.map(d =>
              '<option value="' + escapeHtml(d.letter) + '">' + escapeHtml(d.letter) + ' (' + escapeHtml(d.label || '可移动磁盘') + ')</option>'
            ).join('');
          }
        }

        const nodeUefi = document.getElementById('chainNodeUefi');
        const txtUefi = document.getElementById('chainTextUefi');
        const nodeEfi = document.getElementById('chainNodeEfi');
        const titleEfi = document.getElementById('chainTitleEfi');
        const txtEfi = document.getElementById('chainTextEfi');
        const nodeOs = document.getElementById('chainNodeOs');
        const txtOs = document.getElementById('chainTextOs');
        const arrow1 = document.getElementById('chainArrow1');
        const arrow2 = document.getElementById('chainArrow2');

        const isInstalled = u.mode === 'Mode1_DirectBoot' || u.mode === 'Mode2_UefiShell';
        const isDirty = u.mode === 'DirtyState';

        if (nodeUefi) nodeUefi.className = 'node-icon ' + (u.isUefi ? 'active' : '');
        if (txtUefi) txtUefi.textContent = u.isUefi ? ('UEFI · 安全启动' + (u.secureBoot ? '开启' : '关闭')) : '传统引导';

        if (nodeEfi) nodeEfi.className = 'node-icon ' + (isDirty ? 'warn' : (isInstalled ? 'active' : ''));
        if (titleEfi) titleEfi.textContent = '目标卡型 ' + state.selectedUnlockCard + ' · 通道 ' + state.selectedUnlockChannel;
        if (txtEfi) txtEfi.textContent = valOrUnknown(u.modeLabel);

        if (nodeOs) nodeOs.className = 'node-icon ' + (isInstalled ? 'active' : '');
        if (txtOs) txtOs.textContent = isInstalled ? 'PCIe 与计算限制已突破' : '原始未解锁引导态';

        if (arrow1) arrow1.classList.toggle('active', isInstalled);
        if (arrow2) arrow2.classList.toggle('active', isInstalled);

        const summaryEl = document.getElementById('unlockActionSummaryText');
        if (summaryEl) {
          summaryEl.textContent = '已选 ' + state.selectedUnlockCard + ' 通道 ' + state.selectedUnlockChannel + (needsUsb && usbSelect && usbSelect.value ? (' (' + usbSelect.value + ')') : '') + '，掀开右侧保护盖即可执行。';
        }
      }

      // 3. SPOOF PAGE RENDERER (DYNAMIC OPTGROUP + ARCH MATRIX + HOLOGRAPHIC STAGE)
      function renderSpoofModelsDropdown() {
        const selectEl = document.getElementById('spoofModelSelect');
        const countBadge = document.getElementById('spoofModelCountBadge');
        const recHint = document.getElementById('spoofRecommendedHint');
        const archGrid = document.getElementById('spoofArchMatrixGrid');
        if (!selectEl) return;

        // 型号清单是静态能力表（6 大架构 76 款），不是运行时数据。
        // 宿主暂未单独下发前，直接复用页面内置的那份，避免下拉框空着。
        if (!state.spoofModels && typeof MOCK !== 'undefined' && MOCK.spoofModels) {
          state.spoofModels = MOCK.spoofModels;
        }

        const m = state.spoofModels;
        if (!m || !Array.isArray(m.groups) || m.groups.length === 0) {
          selectEl.innerHTML = '<option value="">等待宿主注入型号清单...</option>';
          if (countBadge) countBadge.textContent = '0 款可选';
          if (archGrid) archGrid.innerHTML = '<div class="state-box" style="grid-column:1/-1;"><div class="state-box-text">等待宿主注入芯片架构分组列表...</div></div>';
          return;
        }

        let total = 0;
        const html = m.groups.map(grp => {
          const models = Array.isArray(grp.models) ? grp.models : [];
          total += models.length;
          if (models.length === 0) return '';
          const opts = models.map(modelName => {
            const isSel = modelName === state.selectedSpoofTarget;
            const recMark = (m.recommended && modelName === m.recommended) ? ' [推荐]' : '';
            return '<option value="' + escapeHtml(modelName) + '"' + (isSel ? ' selected' : '') + '>' +
              escapeHtml(modelName + recMark) + '</option>';
          }).join('');
          return '<optgroup label="' + escapeHtml(grp.label || grp.chip || '分组') + '">' + opts + '</optgroup>';
        }).join('');

        selectEl.innerHTML = html || '<option value="">无匹配型号</option>';
        if (!state.selectedSpoofTarget && selectEl.value) {
          state.selectedSpoofTarget = selectEl.value;
        }
        if (countBadge) countBadge.textContent = '共 ' + total + ' 款型号';
        if (recHint) recHint.textContent = '推荐型号: ' + valOrUnknown(m.recommended);

        if (archGrid) {
          archGrid.innerHTML = m.groups.map(grp => {
            const cnt = Array.isArray(grp.models) ? grp.models.length : 0;
            return '<div class="matrix-item">' +
              '<div class="matrix-icon-box pass">' +
                '<svg class="svg-icon" viewBox="0 0 24 24"><rect class="svg-tone" x="5" y="5" width="14" height="14" rx="2.5"/><rect x="5" y="5" width="14" height="14" rx="2.5"/><rect x="9" y="9" width="6" height="6" rx="1"/></svg>' +
              '</div>' +
              '<div>' +
                '<div class="matrix-title">' + escapeHtml(valOrUnknown(grp.chip)) + ' 架构</div>' +
                '<div class="matrix-sub">' + cnt + ' 款型号</div>' +
              '</div>' +
            '</div>';
          }).join('');
        }
      }

      function renderSpoofPage() {
        renderStateBanner(
          'spoofStateBanner',
          state.spoofStatus,
          state.spoofError,
          'device.refresh',
          '未检测到可伪装的显卡槽位。',
          '重新扫描',
          'device.refresh'
        );

        const sp = state.spoofState;
        const gpus = sp && Array.isArray(sp.gpus) ? sp.gpus : [];
        const slotSelect = document.getElementById('spoofSlotSelect');

        if (slotSelect) {
          if (gpus.length === 0) {
            slotSelect.innerHTML = '<option value="0">未检测到显卡槽位</option>';
          } else {
            slotSelect.innerHTML = gpus.map(g =>
              '<option value="' + escapeHtml(g.slot) + '"' + (Number(g.slot) === Number(state.selectedSpoofSlot) ? ' selected' : '') + '>' +
                '槽位 #' + escapeHtml(g.slot) + ' · ' + escapeHtml(valOrUnknown(g.detectedModel)) +
              '</option>'
            ).join('');
          }
        }

        const currentGpu = gpus.find(g => Number(g.slot) === Number(state.selectedSpoofSlot)) || gpus[0] || null;
        const statusBadge = document.getElementById('spoofCurrentStatusBadge');
        const tdDetected = document.getElementById('spoofTableDetected');
        const tdChip = document.getElementById('spoofTableChip');
        const tdCurDesc = document.getElementById('spoofTableCurrentDesc');
        const tdTarget = document.getElementById('spoofTableTargetName');
        const tdFlag = document.getElementById('spoofTableFlag');

        const srcTitle = document.getElementById('spoofStageSourceTitle');
        const srcSub = document.getElementById('spoofStageSourceSub');
        const tgtTitle = document.getElementById('spoofStageTargetTitle');
        const tgtSub = document.getElementById('spoofStageTargetSub');

        if (!currentGpu) {
          if (statusBadge) { statusBadge.className = 'tag'; statusBadge.textContent = '无数据'; }
          return;
        }

        const effectiveTarget = state.selectedSpoofTarget || currentGpu.targetName;

        if (statusBadge) {
          statusBadge.className = 'tag ' + (currentGpu.isSpoofed ? 'success' : '');
          statusBadge.textContent = currentGpu.isSpoofed ? '当前状态: 已伪装' : '当前状态: 原厂未伪装';
        }
        if (srcTitle) srcTitle.textContent = valOrUnknown(currentGpu.detectedModel) + ' (' + valOrUnknown(currentGpu.chip) + ')';
        if (srcSub) srcSub.textContent = '当前描述: ' + valOrUnknown(currentGpu.currentDesc);
        if (tgtTitle) tgtTitle.textContent = valOrUnknown(effectiveTarget);
        if (tgtSub) tgtSub.textContent = currentGpu.isSpoofed ? '当前已在注册表生效' : '点击应用后免重启热重载生效';

        if (tdDetected) tdDetected.textContent = valOrUnknown(currentGpu.detectedModel);
        if (tdChip) tdChip.textContent = valOrUnknown(currentGpu.chip);
        if (tdCurDesc) tdCurDesc.textContent = valOrUnknown(currentGpu.currentDesc);
        if (tdTarget) tdTarget.textContent = valOrUnknown(effectiveTarget);
        if (tdFlag) tdFlag.textContent = currentGpu.isSpoofed ? ('已伪装为 ' + valOrUnknown(currentGpu.currentDesc)) : '原厂默认';
      }

      // 4. POWER PAGE RENDERER
      function renderPowerPage() {
        renderStateBanner('powerStateBanner', state.powerStatus, state.powerError, 'device.refresh');

        const p = state.powerState;
        if (!p) return;

        /* 把当前档位写到 body，供显卡 3D 风扇等纯 CSS 效果读取：
           body[data-power-preset="sleep|adaptive|turbo"]
           特效只读这个标记调速，不在 JS 里写死转速。 */
        try {
          const preset = (p.preset === 'sleep' || p.preset === 'turbo') ? p.preset : 'adaptive';
          document.body.dataset.powerPreset = preset;
        } catch (e) { }

        const daemonTag = document.getElementById('powerDaemonStatusTag');
        const taskTag = document.getElementById('powerTaskStatusTag');
        const lastTrig = document.getElementById('powerLastTriggerTag');
        const idleInput = document.getElementById('powerIdleSecondsInput');
        const rangeTxt = document.getElementById('powerRangeSummaryText');
        const pstateTag = document.getElementById('powerCurrentPstateTag');

        if (daemonTag) {
          daemonTag.className = 'tag ' + (p.running ? 'success' : (p.enabled ? 'warning' : ''));
          daemonTag.textContent = '守护进程: ' + (p.running ? '运行中' : (p.enabled ? '已配置待命' : '已关闭'));
        }
        if (taskTag) {
          taskTag.className = 'tag ' + (p.taskInstalled ? 'accent' : '');
          taskTag.textContent = '计划任务: ' + (p.taskInstalled ? '已注册' : '未注册');
        }
        if (lastTrig) lastTrig.textContent = '最近触发: ' + valOrUnknown(p.lastTrigger);
        if (idleInput && document.activeElement !== idleInput && p.idleSeconds) {
          idleInput.value = p.idleSeconds;
        }
        if (pstateTag) {
          pstateTag.className = 'tag accent';
          pstateTag.textContent = '当前状态: ' + valOrUnknown(p.currentPstate);
        }

        const pr = p.powerRange || {};
        if (rangeTxt) {
          rangeTxt.textContent = '范围 ' + valOrUnknown(pr.min, 'W') + '~' + valOrUnknown(pr.max, 'W') + ' · 当前 ' + valOrUnknown(pr.current, 'W');
        }

        ['sleep', 'adaptive', 'turbo'].forEach(mode => {
          const card = document.getElementById('presetCard-' + mode);
          if (card) {
            card.classList.toggle('active', p.preset === mode);
            card.classList.toggle('turbo-mode', p.preset === 'turbo' && mode === 'turbo');
          }
        });

        const animStage = document.getElementById('powerAnimStage');
        const animBadgeTxt = document.getElementById('powerAnimBadgeText');
        const animTitle = document.getElementById('powerAnimTitle');
        const animSub = document.getElementById('powerAnimSub');
        const animDot = document.getElementById('powerAnimDot');
        const reactorVal = document.getElementById('powerReactorValText');
        const reactorSub = document.getElementById('powerReactorSubText');

        if (animStage) {
          const nextPreset = p.preset || 'adaptive';
          const presetChanged = !!state._lastPowerPreset && state._lastPowerPreset !== nextPreset;
          animStage.classList.remove('preset-sleep', 'preset-adaptive', 'preset-turbo');
          animStage.classList.add('preset-' + nextPreset);
          state._lastPowerPreset = nextPreset;
          /* 换挡时数字与文案做一次轻微滚动重排，避免生硬替换 */
          if (presetChanged) {
            const swapTargets = [
              animStage.querySelector('.turbo-data-readout'),
              animStage.querySelector('.anim-left')
            ];
            swapTargets.forEach(el => {
              if (!el) return;
              el.classList.remove('swapping');
              void el.offsetWidth;
              el.classList.add('swapping');
              window.setTimeout(() => el.classList.remove('swapping'), 460);
            });
          }
        }

        if (p.preset === 'turbo') {
          if (animDot) animDot.style.background = 'var(--danger)';
          if (animBadgeTxt) animBadgeTxt.textContent = '满血狂暴 · P0 极限超频聚变已激活';
          if (animTitle) animTitle.textContent = '全核高频释放 · 功耗上限解锁至 ' + valOrUnknown(pr.current || pr.max, 'W');
          if (reactorVal) reactorVal.textContent = valOrUnknown(pr.current || 320) + ' W';
          if (reactorSub) { reactorSub.textContent = '2100 MHz'; }
        } else if (p.preset === 'sleep') {
          if (animDot) animDot.style.background = 'var(--success)';
          if (animBadgeTxt) animBadgeTxt.textContent = '极致静音 · P8 冰封待机已锁定';
          if (animTitle) animTitle.textContent = '超低频静音待机 · 状态 ' + valOrUnknown(p.currentPstate);
          if (reactorVal) reactorVal.textContent = valOrUnknown(pr.current || 36.5) + ' W';
          if (reactorSub) { reactorSub.textContent = '310 MHz'; }
        } else {
          if (animDot) animDot.style.background = 'var(--accent)';
          if (animBadgeTxt) animBadgeTxt.textContent = '智能动态 · 空闲 ' + valOrUnknown(p.idleSeconds, '秒') + ' 自动切入 P8';
          if (animTitle) animTitle.textContent = '毫秒级自适应变频调度 · 状态 ' + valOrUnknown(p.currentPstate);
          if (reactorVal) reactorVal.textContent = valOrUnknown(pr.current || 148.5) + ' W';
          if (reactorSub) { reactorSub.textContent = '1710 MHz'; }
        }
        if (animSub) {
          animSub.textContent = '功耗下限 ' + valOrUnknown(pr.min, 'W') +
            ' / 默认 ' + valOrUnknown(pr.default, 'W') +
            ' / 最大 ' + valOrUnknown(pr.max, 'W') +
            ' / 当前设定 ' + valOrUnknown(pr.current, 'W');
        }
        if (typeof window.__ensurePowerAnimLoop === 'function') {
          window.__ensurePowerAnimLoop();
        }
      }

      // 5. DLSS PAGE RENDERER
      function renderDlssPage() {
        renderStateBanner('dlssStateBanner', state.dlssStatus, state.dlssError, 'dlss.checkUpdate');

        const d = state.dlssState;
        if (!d) return;

        const verBadge = document.getElementById('dlssVersionBadge');
        const rtPath = document.getElementById('dlssRuntimePathText');
        const mirrorSelect = document.getElementById('dlssMirrorSelect');
        const proxySelect = document.getElementById('dlssProxySelect');
        const gameDirInput = document.getElementById('dlssGameDirInput');

        if (verBadge) {
          verBadge.className = 'tag ' + (d.hasUpdate ? 'warning' : (d.runtimeFound ? 'success' : 'danger'));
          verBadge.textContent = 'v' + valOrUnknown(d.localVersion) + ' / v' + valOrUnknown(d.latestVersion);
        }
        if (rtPath) {
          rtPath.textContent = valOrUnknown(d.runtimePath);
        }
        if (mirrorSelect) {
          const mirrors = Array.isArray(d.mirrors) ? d.mirrors : [];
          mirrorSelect.innerHTML = mirrors.length > 0
            ? mirrors.map((m, idx) =>
                '<option value="' + idx + '">' + escapeHtml(m.name) + ' (' + escapeHtml(m.latencyMs) + 'ms)</option>'
              ).join('')
            : '<option value="0">默认镜像</option>';
        }
        if (proxySelect) {
          const cands = Array.isArray(d.proxyCandidates) && d.proxyCandidates.length > 0
            ? d.proxyCandidates
            : ['nvngx.dll', 'dxgi.dll', 'winmm.dll'];
          const prevVal = proxySelect.value;
          proxySelect.innerHTML = cands.map(c =>
            '<option value="' + escapeHtml(c) + '"' + (c === prevVal ? ' selected' : '') + '>' + escapeHtml(c) + '</option>'
          ).join('');
        }
        if (gameDirInput && !gameDirInput.value && d.gameDir) {
          gameDirInput.value = d.gameDir;
        }

        renderDlssPreflightSection();
      }

      function renderDlssPreflightSection() {
        const pf = state.dlssPreflight;
        const summaryBadge = document.getElementById('dlssPreflightSummaryBadge');
        const blockBadge = document.getElementById('dlssBlockingBadge');
        const container = document.getElementById('dlssPreflightItemsContainer');
        const deployBtn = document.getElementById('btnDlssDeploy');

        if (!pf || !Array.isArray(pf.items)) {
          if (summaryBadge) { summaryBadge.className = 'tag'; summaryBadge.textContent = '前置检查: 未执行'; }
          if (blockBadge) { blockBadge.className = 'tag'; blockBadge.textContent = '阻塞项: --'; }
          return;
        }

        const hasBlockingFailure = pf.items.some(item => !item.ok && item.blocking) || (pf.blockingCount > 0);
        if (deployBtn) {
          deployBtn.disabled = hasBlockingFailure;
        }
        if (summaryBadge) {
          summaryBadge.className = 'tag ' + (pf.passed && !hasBlockingFailure ? 'success' : 'danger');
          summaryBadge.textContent = pf.passed && !hasBlockingFailure ? '已通过' : '未通过';
        }
        if (blockBadge) {
          blockBadge.className = 'tag ' + (hasBlockingFailure ? 'danger' : 'success');
          blockBadge.textContent = '阻塞 ' + valOrUnknown(pf.blockingCount);
        }
        if (container) {
          container.innerHTML = pf.items.map(item => {
            const isBlockFail = !item.ok && item.blocking;
            const toneClass = item.ok ? 'pass' : (item.blocking ? 'danger' : 'warn');
            const borderStyle = isBlockFail ? 'border-color:var(--danger);background:var(--danger-soft);' : '';
            return '<div class="matrix-item" style="' + borderStyle + '">' +
              '<div class="matrix-icon-box ' + toneClass + '">' +
                '<svg class="svg-icon" viewBox="0 0 24 24">' +
                  (item.ok ? '<circle class="svg-tone" cx="12" cy="12" r="8.5"/><path d="M5.5 12.5l4.5 4.5L19 7.5"/>' : '<circle class="svg-tone" cx="12" cy="12" r="8.5"/><circle cx="12" cy="12" r="8.5"/><path d="M12 8.5v4M12 15.5h.01"/>') +
                '</svg>' +
              '</div>' +
              '<div style="flex:1;">' +
                '<div class="matrix-title">' + escapeHtml(item.label) + '</div>' +
                '<div class="matrix-sub">' + escapeHtml(valOrUnknown(item.detail)) + '</div>' +
              '</div>' +
            '</div>';
          }).join('');
        }
      }

      // 6. OVERLAY PAGE RENDERER (ENRICHED HUD PREVIEW + 3-STAGE TOPOLOGY + MATRIX)
      function renderOverlayPage() {
        renderStateBanner('overlayStateBanner', state.overlayStatus, state.overlayError, 'device.refresh');

        const ov = state.overlayState;
        if (!ov) return;

        const badge = document.getElementById('overlayInstallBadge');
        const backupTag = document.getElementById('overlayBackupTag');
        const pathTxt = document.getElementById('overlayPathText');
        const grid = document.getElementById('overlayMatrixGrid');

        // HUD Preview Cards
        const altZBox = document.getElementById('overlayAltZIconBox');
        const altZSub = document.getElementById('overlayAltZSubText');
        const altZPill = document.getElementById('overlayAltZPill');
        const altRBox = document.getElementById('overlayAltRIconBox');
        const altRSub = document.getElementById('overlayAltRSubText');
        const altRPill = document.getElementById('overlayAltRPill');
        const cefHud = document.getElementById('overlayCefHudText');

        if (altZBox) altZBox.className = 'matrix-icon-box ' + (ov.altZReady ? 'pass' : 'warn');
        if (altZSub) altZSub.textContent = ov.altZReady ? '面板已解锁就绪' : '受白名单限制，请点击解除白名单';
        if (altZPill) {
          altZPill.className = 'tag ' + (ov.altZReady ? 'success' : 'warning');
          altZPill.textContent = ov.altZReady ? '已就绪' : '未解锁';
        }

        if (altRBox) altRBox.className = 'matrix-icon-box ' + (ov.altRReady ? 'pass' : 'warn');
        if (altRSub) altRSub.textContent = ov.altRReady ? '性能浮层已就绪' : '待解除白名单';
        if (altRPill) {
          altRPill.className = 'tag ' + (ov.altRReady ? 'success' : 'warning');
          altRPill.textContent = ov.altRReady ? '已就绪' : '未解锁';
        }
        if (cefHud) cefHud.textContent = valOrUnknown(ov.cefCacheSize);

        const nInst = document.getElementById('overlayNodeInstall');
        const nInstSub = document.getElementById('overlayNodeInstallSub');
        const nPatch = document.getElementById('overlayNodePatch');
        const nPatchSub = document.getElementById('overlayNodePatchSub');
        const nHud = document.getElementById('overlayNodeHud');
        const nHudSub = document.getElementById('overlayNodeHudSub');
        const arr1 = document.getElementById('overlayArrow1');
        const arr2 = document.getElementById('overlayArrow2');

        if (nInst) nInst.className = 'node-icon ' + (ov.nvAppInstalled ? 'active' : '');
        if (nInstSub) nInstSub.textContent = ov.nvAppInstalled ? '已安装' : '未安装';
        if (nPatch) nPatch.className = 'node-icon ' + (ov.patched ? 'active' : 'warn');
        if (nPatchSub) nPatchSub.textContent = ov.patched ? '已解除限制' : '未解锁';
        if (nHud) nHud.className = 'node-icon ' + ((ov.altRReady && ov.altZReady) ? 'active' : '');
        if (nHudSub) nHudSub.textContent = (ov.altRReady && ov.altZReady) ? '已就绪' : '待解锁';
        if (arr1) arr1.classList.toggle('active', !!ov.nvAppInstalled);
        if (arr2) arr2.classList.toggle('active', !!ov.patched);

        if (badge) {
          badge.className = 'tag ' + (ov.nvAppInstalled ? 'success' : 'danger');
          badge.textContent = ov.nvAppInstalled ? 'NVIDIA App 已安装' : '未检测到 NVIDIA App';
        }
        if (backupTag) {
          backupTag.className = 'tag ' + (ov.backupExists ? 'accent' : '');
          backupTag.textContent = '备份: ' + (ov.backupExists ? '完好' : '无');
        }
        if (pathTxt) {
          pathTxt.textContent = valOrUnknown(ov.nvAppPath);
        }
        if (grid) {
          const items = [
            { title: '白名单补丁', sub: ov.patched ? '已注入' : '未注入', tone: ov.patched ? 'pass' : 'warn' },
            { title: '原始文件备份', sub: ov.backupExists ? '支持一键还原' : '无备份', tone: ov.backupExists ? 'pass' : 'warn' },
            { title: '浏览器缓存', sub: valOrUnknown(ov.cefCacheSize), tone: 'pass' },
            { title: 'Alt+R 性能浮层', sub: ov.altRReady ? '可用' : '未解锁', tone: ov.altRReady ? 'pass' : 'warn' },
            { title: 'Alt+Z 覆盖面板', sub: ov.altZReady ? '可用' : '未解锁', tone: ov.altZReady ? 'pass' : 'warn' }
          ];
          grid.innerHTML = items.map(c =>
            '<div class="matrix-item">' +
              '<div class="matrix-icon-box ' + c.tone + '">' +
                '<svg class="svg-icon" viewBox="0 0 24 24"><circle class="svg-tone" cx="12" cy="12" r="8.5"/><path d="M5.5 12.5l4.5 4.5L19 7.5"/></svg>' +
              '</div>' +
              '<div>' +
                '<div class="matrix-title">' + escapeHtml(c.title) + '</div>' +
                '<div class="matrix-sub">' + escapeHtml(c.sub) + '</div>' +
              '</div>' +
            '</div>'
          ).join('');
        }
      }

      // 7. DUALGPU PAGE RENDERER (ENRICHED 4-CARD SUMMARY + CASO TOPOLOGY + TABLE)
      function renderDualPage() {
        renderStateBanner('dualStateBanner', state.dualStatus, state.dualError, 'dual.scanGames');

        const dl = state.dualState;
        if (!dl) return;

        /* 磁盘选择器：由宿主的 dual.state.drives 填充。
           用户可指定只扫某个盘，默认「全部磁盘」。 */
        try {
          const driveSel = document.getElementById('dualScanDrive');
          if (driveSel && Array.isArray(dl.drives) && dl.drives.length > 0) {
            const keep = driveSel.value;
            driveSel.innerHTML = '<option value="">全部磁盘</option>' +
              dl.drives.map(function (d) {
                const label = d.label ? ' ' + d.label : '';
                return '<option value="' + escapeHtml(d.letter) + '">' +
                  escapeHtml(d.letter + label + ' · ' + d.freeGB + 'GB 可用') + '</option>';
              }).join('');
            if (keep) driveSel.value = keep;
          }
        } catch (e) { }

        // 4-Card Summary
        const icRender = document.getElementById('dualCardIconRender');
        const txRender = document.getElementById('dualCardTextRender');
        const sbRender = document.getElementById('dualCardSubRender');

        const icDisp = document.getElementById('dualCardIconDisplay');
        const txDisp = document.getElementById('dualCardTextDisplay');
        const sbDisp = document.getElementById('dualCardSubDisplay');

        const icHags = document.getElementById('dualCardIconHags');
        const txHags = document.getElementById('dualCardTextHags');

        const icVirt = document.getElementById('dualCardIconVirtual');
        const txVirt = document.getElementById('dualCardTextVirtual');
        const sbVirt = document.getElementById('dualCardSubVirtual');

        if (icRender) icRender.className = 'matrix-icon-box ' + (dl.hasRenderGpu ? 'pass' : 'danger');
        if (txRender) txRender.textContent = dl.hasRenderGpu ? valOrUnknown(dl.renderGpuName) : '未检测到';
        if (sbRender) sbRender.textContent = valOrUnknown(dl.renderGpuPciId);

        if (icDisp) icDisp.className = 'matrix-icon-box ' + (dl.hasDisplayGpu ? 'pass' : 'warn');
        if (txDisp) txDisp.textContent = dl.hasDisplayGpu ? valOrUnknown(dl.displayGpuName) : '未检测到';
        if (sbDisp) sbDisp.textContent = valOrUnknown(dl.displayGpuPciId);

        if (icHags) icHags.className = 'matrix-icon-box ' + (dl.hagsActive ? 'pass' : 'warn');
        if (txHags) txHags.textContent = dl.hagsActive ? '已开启' : '未开启';

        if (icVirt) icVirt.className = 'matrix-icon-box ' + (dl.hasVirtualGpu ? 'pass' : '');
        if (txVirt) txVirt.textContent = dl.hasVirtualGpu ? valOrUnknown(dl.virtualGpuName) : '未挂载';
        if (sbVirt) sbVirt.textContent = '已绑定 ' + valOrUnknown(dl.boundGamesCount, ' 款');

        const hagsBadge = document.getElementById('dualHagsBadge');
        const rName = document.getElementById('dualRenderGpuName');
        const rPci = document.getElementById('dualRenderGpuPci');
        const dName = document.getElementById('dualDisplayGpuName');
        const dPci = document.getElementById('dualDisplayGpuPci');
        const vInfo = document.getElementById('dualVirtualGpuInfo');
        const cntBadge = document.getElementById('dualBoundCountBadge');
        const listEl = document.getElementById('dualBoundListContainer');

        if (hagsBadge) {
          hagsBadge.className = 'tag ' + (dl.hagsActive ? 'success' : 'warning');
          hagsBadge.textContent = 'HAGS: ' + (dl.hagsActive ? '已开启' : '未开启');
        }
        if (rName) rName.textContent = dl.hasRenderGpu ? valOrUnknown(dl.renderGpuName) : '未检测到';
        if (rPci) rPci.textContent = valOrUnknown(dl.renderGpuPciId);
        if (dName) dName.textContent = dl.hasDisplayGpu ? valOrUnknown(dl.displayGpuName) : '未检测到';
        if (dPci) dPci.textContent = valOrUnknown(dl.displayGpuPciId);
        if (vInfo) vInfo.textContent = dl.hasVirtualGpu ? valOrUnknown(dl.virtualGpuName) : '直连输出';
        if (cntBadge) cntBadge.textContent = '共 ' + valOrUnknown(dl.boundGamesCount, ' 款');

        if (!listEl) return;
        const apps = Array.isArray(dl.boundApps) ? dl.boundApps : [];
        if (apps.length === 0) {
          listEl.innerHTML = '<div class="state-box"><div class="state-box-text">暂无绑定游戏</div>' +
            '<button class="btn btn-primary" data-action="dual.scanGames">扫描常见游戏</button></div>';
          return;
        }

        listEl.innerHTML = '<table class="data-table"><thead><tr><th title="name">程序名</th><th title="exePath">路径</th><th title="addedAt">添加日期</th><th>操作</th></tr></thead><tbody>' +
          apps.map(app =>
            '<tr>' +
              '<td><strong>' + escapeHtml(valOrUnknown(app.name)) + '</strong></td>' +
              '<td>' + escapeHtml(valOrUnknown(app.exePath)) + '</td>' +
              '<td>' + escapeHtml(valOrUnknown(app.addedAt)) + '</td>' +
              '<td><button class="btn btn-secondary" style="padding:4px 8px;font-size:11.5px;" data-action="dual.unbind" data-exe-path="' + escapeHtml(app.exePath) + '">解绑</button></td>' +
            '</tr>'
          ).join('') +
          '</tbody></table>';
      }

      // 8. MAINTENANCE PAGE RENDERER (ENRICHED WAL STORAGE SUMMARY + KIND FILTER + TIMELINE)
      function renderMaintenancePage() {
        if (state.maintenanceStatus === 'error') {
          renderStateBanner('maintenanceStateBanner', 'error', state.maintenanceError, 'device.refresh');
        } else {
          const b = document.getElementById('maintenanceStateBanner');
          if (b) b.innerHTML = '';
        }

        const allList = state.snapshots || [];
        const filterKind = state.snapshotFilterKind || 'all';
        const list = filterKind === 'all' ? allList : allList.filter(i => i.kind === filterKind);

        const cntBadge = document.getElementById('snapshotCountBadge');
        const storageTag = document.getElementById('maintenanceStorageSummaryTag');
        const container = document.getElementById('snapshotTimelineContainer');

        const totalBytes = allList.reduce((acc, i) => acc + (Number(i.sizeBytes) || 0), 0);
        if (storageTag) {
          storageTag.textContent = allList.length + ' 个 (' + (totalBytes / 1024).toFixed(0) + ' KB)';
        }
        if (cntBadge) {
          cntBadge.className = 'tag ' + (list.length > 0 ? 'accent' : '');
          cntBadge.textContent = list.length + ' / ' + allList.length;
        }
        if (!container) return;

        if (state.maintenanceStatus === 'loading') {
          container.innerHTML = '<div class="skeleton-line" style="height:48px;"></div>';
          return;
        }

        if (allList.length === 0) {
          container.innerHTML = '<div class="state-box">' +
            '<div class="state-box-text">暂无快照</div>' +
            '<button class="btn btn-primary" data-action="snapshot.create">创建快照</button>' +
          '</div>';
          return;
        }

        if (list.length === 0) {
          container.innerHTML = '<div class="state-box"><div class="state-box-text">该分类暂无快照</div></div>';
          return;
        }

        const kindMap = { auto: '自动', factory: '出厂', manual: '手动' };
        container.innerHTML = list.map(item => {
          const kb = item.sizeBytes ? (Number(item.sizeBytes) / 1024).toFixed(0) + ' KB' : '--';
          const kLabel = kindMap[item.kind] || item.kind;
          return '<div class="timeline-item" title="' + escapeHtml(valOrUnknown(item.id)) + '">' +
            '<div>' +
              '<div class="timeline-time">' + escapeHtml(valOrUnknown(item.createdAt)) + '</div>' +
              '<div class="timeline-title">' + escapeHtml(valOrUnknown(item.label)) + '</div>' +
              '<div class="timeline-sub">' + escapeHtml(kLabel) + ' · ' + escapeHtml(kb) + '</div>' +
            '</div>' +
            '<div style="display:flex;gap:8px;">' +
              '<button class="btn btn-secondary" data-action="modal.confirm" data-confirm-type="snapshot.restore" data-snapshot-id="' + escapeHtml(item.id) + '"' + (!item.restorable ? ' disabled' : '') + '>回滚</button>' +
              '<button class="btn btn-secondary" data-action="snapshot.delete" data-snapshot-id="' + escapeHtml(item.id) + '">删除</button>' +
            '</div>' +
          '</div>';
        }).join('');
      }

      // 9. LOGS PAGE RENDERER (ENRICHED 4-CARD TELEMETRY COUNTERS + STREAM VIEWER)
      function renderLogsPage(isAppendOnly) {
        renderStateBanner('logsStateBanner', state.logsStatus, state.logsError, 'logs.loadMore');

        const box = document.getElementById('logStreamBox');
        const cntTxt = document.getElementById('logsCountText');

        // Update 4-Card Live Counters
        const statAll = document.getElementById('logsStatAll');
        const statAudit = document.getElementById('logsStatAudit');
        const statWarn = document.getElementById('logsStatWarn');
        const statError = document.getElementById('logsStatError');
        const allLogs = state.logsList || [];

        if (statAll) statAll.textContent = String(allLogs.length);
        if (statAudit) statAudit.textContent = String(allLogs.filter(x => x.level === 'audit').length);
        if (statWarn) statWarn.textContent = String(allLogs.filter(x => x.level === 'warn').length);
        if (statError) statError.textContent = String(allLogs.filter(x => x.level === 'error').length);

        if (!box) return;

        const lvlFilter = state.logsFilterLevel;
        const kw = (state.logsSearchKeyword || '').trim().toLowerCase();

        const filtered = allLogs.filter(item => {
          if (lvlFilter !== 'all' && item.level !== lvlFilter) return false;
          if (kw && !(String(item.text || '').toLowerCase().includes(kw) || String(item.at || '').toLowerCase().includes(kw))) return false;
          return true;
        });

        if (cntTxt) {
          cntTxt.textContent = '当前显示 ' + filtered.length + ' 条 / 总计缓存 ' + allLogs.length + ' 条';
        }

        if (filtered.length === 0) {
          box.innerHTML = '<div class="state-box" style="margin:auto;"><div class="state-box-text">当前过滤条件下暂无日志记录。</div></div>';
          return;
        }

        box.innerHTML = filtered.map(item =>
          '<div class="log-row">' +
            '<span class="log-time">[' + escapeHtml(valOrUnknown(item.at)) + ']</span>' +
            '<span class="log-lvl ' + escapeHtml(item.level || 'info') + '">' + escapeHtml(item.level || 'info') + '</span>' +
            '<span class="log-txt">' + escapeHtml(item.text || '') + '</span>' +
          '</div>'
        ).join('');

        if (state.logsAutoScroll || !isAppendOnly) {
          box.scrollTop = box.scrollHeight;
        }
      }

      // 10. UPDATE PAGE RENDERER (STRICT verified GATE + 3-STAGE PIPELINE)
      function renderUpdatePage() {
        renderStateBanner('updateStateBanner', state.updateStatus, state.updateError, 'update.check');

        const u = state.updateState;
        const applyBtn = document.getElementById('btnUpdateApply');
        if (!u) {
          if (applyBtn) applyBtn.disabled = true;
          return;
        }

        const hasUpBadge = document.getElementById('updateHasUpdateBadge');
        const curVer = document.getElementById('updateCurrentVerText');
        const latVer = document.getElementById('updateLatestVerText');
        const assetTxt = document.getElementById('updateAssetNameText');
        const shaTxt = document.getElementById('updateSha256Text');
        const verTxt = document.getElementById('updateVerifiedText');
        const notesBox = document.getElementById('updateReleaseNotesBox');

        // 3-Stage Verification Pipeline Nodes
        const nDl = document.getElementById('updateNodeDownload');
        const nAssetTitle = document.getElementById('updateNodeAssetTitle');
        const nAssetSub = document.getElementById('updateNodeAssetSub');
        const arrVer = document.getElementById('updateArrowVerify');
        const nVer = document.getElementById('updateNodeVerify');
        const nVerSub = document.getElementById('updateNodeVerifySub');

        const isVerified = u.verified === true;
        if (nDl) nDl.className = 'node-icon ' + (u.assetName ? 'active' : '');
        if (nAssetTitle) nAssetTitle.textContent = u.assetName ? u.assetName : '安装包';
        if (nAssetSub) nAssetSub.textContent = u.hasUpdate ? ('v' + valOrUnknown(u.latestVersion)) : '已是最新';
        if (arrVer) arrVer.classList.toggle('active', isVerified);
        if (nVer) nVer.className = 'node-icon ' + (isVerified ? 'active' : 'warn');
        if (nVerSub) nVerSub.textContent = isVerified ? '已通过' : '待校验';

        if (hasUpBadge) {
          hasUpBadge.className = 'tag ' + (u.hasUpdate ? 'warning' : 'success');
          hasUpBadge.textContent = u.hasUpdate ? '有新版本' : '已是最新';
        }
        if (curVer) curVer.textContent = valOrUnknown(u.currentVersion);
        if (latVer) latVer.textContent = valOrUnknown(u.latestVersion);
        if (assetTxt) assetTxt.textContent = valOrUnknown(u.assetName);
        if (shaTxt) shaTxt.textContent = valOrUnknown(u.sha256);

        // HARD SECURITY GATE: verified === false MUST disable apply button
        if (applyBtn) {
          applyBtn.disabled = !isVerified;
        }
        if (verTxt) {
          verTxt.innerHTML = isVerified
            ? '<span class="tag success" title="verified: true">校验通过</span>'
            : '<span class="tag danger" title="verified: false">待校验</span>';
        }
        if (notesBox) {
          notesBox.textContent = valOrUnknown(u.releaseNotes);
        }
      }

      /* =======================================================================
         SECTION 6: MODAL, NAVIGATION, KEYBOARD & EVENT DELEGATION
         ======================================================================= */
      function switchPage(pageId) {
        state.currentPage = pageId;
        document.querySelectorAll('.view-panel').forEach(panel => {
          panel.classList.toggle('active', panel.id === 'view-' + pageId);
        });
        document.querySelectorAll('#sidebarNavList .nav-item').forEach(btn => {
          const isActive = btn.dataset.page === pageId;
          btn.classList.toggle('active', isActive);
          if (isActive) {
            btn.classList.remove('is-popping');
            void btn.offsetWidth; // 强制重排，让每次点击都能重放图标回弹
            btn.classList.add('is-popping');
            window.setTimeout(() => btn.classList.remove('is-popping'), 480);
          }
        });
        if (pageId === 'power' && typeof window.__ensurePowerAnimLoop === 'function') {
          window.__ensurePowerAnimLoop();
        }
      }

      function applyThemeVisual(theme) {
        state.theme = theme;
        document.body.classList.remove('theme-dark', 'theme-light');
        document.body.classList.add('theme-' + theme);
        if (isPerfLow) document.body.classList.add('perf-low');
        const lbl = document.getElementById('themeBtnLabel');
        if (lbl) lbl.textContent = theme === 'light' ? '极简白昼' : '曜石黑夜';
        const edTag = document.getElementById('gpu3dEditionTag');
        const plateTxt = document.getElementById('gpu3dPlateText');
        if (edTag) edTag.textContent = theme === 'light' ? 'WHITE FROST 3X' : 'OBSIDIAN 3X';
        if (plateTxt) plateTxt.textContent = theme === 'light' ? 'WHITE FROST · TRIPLE FAN OC' : 'OBSIDIAN GOLD · TRIPLE FAN OC';
      }

      function togglePerfLowMode() {
        isPerfLow = !isPerfLow;
        document.body.classList.toggle('perf-low', isPerfLow);
        const btn = document.getElementById('btnTogglePerfLow');
        const lbl = document.getElementById('perfLowBtnLabel');
        if (btn) btn.classList.toggle('active', isPerfLow);
        if (lbl) lbl.textContent = isPerfLow ? '低功耗已开' : '标准动效';
        showToast(isPerfLow ? '已开启低功耗渲染模式' : '已恢复标准动效模式', 'info');
      }

      function getDlssPreflightPayload() {
        const gameDir = (document.getElementById('dlssGameDirInput') || {}).value || '';
        const proxyName = (document.getElementById('dlssProxySelect') || {}).value || 'nvngx.dll';
        const injectRe = ((document.getElementById('dlssReFrameworkSelect') || {}).value === 'true');
        return { gameDir: gameDir.trim(), proxyName: proxyName, injectReFramework: injectRe };
      }

      // Custom Confirmation Modal (Section 6.2: what changes, impact, reversible, reboot, plus RESET input)
      function openDangerConfirmModal(confirmType, triggerEl) {
        const modal = document.getElementById('actionModal');
        const titleEl = document.getElementById('modalTitle');
        const chgEl = document.getElementById('modalWhatChanges');
        const impEl = document.getElementById('modalWhatImpacts');
        const revEl = document.getElementById('modalReversible');
        const rebEl = document.getElementById('modalNeedReboot');
        const resetWrap = document.getElementById('modalResetInputWrap');
        const resetInput = document.getElementById('modalResetConfirmInput');
        const confirmBtn = document.getElementById('modalConfirmBtn');

        if (!modal) return;
        resetWrap.style.display = 'none';
        if (resetInput) resetInput.value = '';
        if (confirmBtn) confirmBtn.disabled = false;

        if (confirmType === 'unlock.install') {
          if (!state.safetyCoverOpen) {
            showToast('请先点击掀开红黑斜纹机械安全保护盖！', 'warn');
            return;
          }
          const usbVal = (document.getElementById('unlockUsbSelect') || {}).value || '';
          const payload = { cardType: state.selectedUnlockCard, channel: state.selectedUnlockChannel };
          if (state.selectedUnlockChannel === 'B' || state.selectedUnlockChannel === 'C') {
            payload.usbDrive = usbVal;
          }
          titleEl.textContent = '确认执行固件引导注入';
          chgEl.textContent = '针对卡型 [' + payload.cardType + '] 执行通道 [' + payload.channel + '] 引导载荷部署与 BCD 引导项配置。';
          impEl.textContent = '解除 PCIe 链路代数与计算模式限制；若引导配置异常将触发 BCD 备份回退。';
          revEl.textContent = '可通过恢复引导或系统快照无损撤销。';
          rebEl.textContent = '是（引导层修改需重启系统后在 UEFI 阶段生效）。';
          state.pendingConfirm = { action: 'unlock.install', payload: payload, triggerBtn: triggerEl };
        } else if (confirmType === 'unlock.rollback') {
          const payload = { cardType: state.selectedUnlockCard };
          titleEl.textContent = '确认恢复官方引导';
          chgEl.textContent = '移除卡型 [' + payload.cardType + '] 的自定义 EFI 引导载荷，恢复 Windows 原始 bootmgfw.efi 引导链。';
          impEl.textContent = '重启后显卡将回到未解锁前的原厂默认 PCIe 状态。';
          revEl.textContent = '可再次执行引导注入重新开启。';
          rebEl.textContent = '是（重启后生效）。';
          state.pendingConfirm = { action: 'unlock.rollback', payload: payload, triggerBtn: triggerEl };
        } else if (confirmType === 'spoof.apply') {
          const targetName = (document.getElementById('spoofModelSelect') || {}).value || state.selectedSpoofTarget;
          if (!targetName) {
            showToast('请先选择目标伪装型号', 'warn');
            return;
          }
          const payload = { gpuSlot: Number(state.selectedSpoofSlot) || 0, targetName: targetName };
          titleEl.textContent = '确认应用显卡伪装';
          chgEl.textContent = '修改槽位 #' + payload.gpuSlot + ' 的 HKLM 驱动类注册表项 DriverDesc 为 [' + targetName + ']。';
          impEl.textContent = '自动触发显卡驱动免重启重载，设备管理器与游戏立即识别新型号。';
          revEl.textContent = '支持一键还原出厂型号名或通过快照回滚。';
          rebEl.textContent = '否（支持 PnP 免重启热重载；若被占用则提示重启）。';
          state.pendingConfirm = { action: 'spoof.apply', payload: payload, triggerBtn: triggerEl };
        } else if (confirmType === 'snapshot.restore') {
          const snapId = triggerEl.dataset.snapshotId || '';
          titleEl.textContent = '确认回滚系统快照';
          chgEl.textContent = '将注册表键值与引导配置恢复至快照 [' + snapId + '] 备份时的完整状态。';
          impEl.textContent = '该快照创建之后的所有伪装或解锁改动将被覆盖。';
          revEl.textContent = '建议回滚前先创建当前状态快照以便双向切换。';
          rebEl.textContent = '视快照包含的引导项改动而定（纯注册表无需重启，含 BCD 改动需重启）。';
          state.pendingConfirm = { action: 'snapshot.restore', payload: { snapshotId: snapId }, triggerBtn: triggerEl };
        } else if (confirmType === 'dlss.remove') {
          const gameDir = ((document.getElementById('dlssGameDirInput') || {}).value || '').trim();
          titleEl.textContent = '确认移除 DLSS 补丁';
          chgEl.textContent = '删除目录 [' + (gameDir || '未指定') + '] 下部署的代理 DLL 与帧生成运行库文件。';
          impEl.textContent = '游戏将恢复为原厂纯净渲染管线，不再启用社区解锁插帧。';
          revEl.textContent = '可随时点击 [一键部署到游戏] 重新注入。';
          rebEl.textContent = '否（重启游戏即可生效）。';
          state.pendingConfirm = { action: 'dlss.remove', payload: { gameDir: gameDir }, triggerBtn: triggerEl };
        } else if (confirmType === 'overlay.restore') {
          titleEl.textContent = '确认还原游戏覆盖配置';
          chgEl.textContent = '使用备份文件还原 NVIDIA App 原始组件配置。';
          impEl.textContent = '还原后未伪装矿卡可能无法直接呼出 Alt+Z / Alt+R 覆盖面板。';
          revEl.textContent = '可随时再次点击 [解除覆盖白名单]。';
          rebEl.textContent = '否（重启 NVIDIA App 即可生效）。';
          state.pendingConfirm = { action: 'overlay.restore', payload: {}, triggerBtn: triggerEl };
        } else if (confirmType === 'factory.reset') {
          titleEl.textContent = '确认执行紧急出厂重置';
          chgEl.textContent = '清除全部显卡伪装注册表项、卸载全部 EFI 引导载荷、恢复原始 BCD 引导、移除省电计划任务。';
          impEl.textContent = '所有显卡与引导配置回到出厂初始状态。';
          revEl.textContent = '不可直接撤销（除非手动重新配置各项功能）。';
          rebEl.textContent = '是（重置完成后强烈建议重启系统）。';
          resetWrap.style.display = 'block';
          if (confirmBtn) confirmBtn.disabled = true;
          state.pendingConfirm = { action: 'factory.reset', payload: { confirmText: '' }, triggerBtn: triggerEl };
        }

        modal.classList.add('open');
        if (confirmType === 'factory.reset' && resetInput) {
          setTimeout(() => resetInput.focus(), 50);
        }
      }

      function closeDangerModal() {
        const modal = document.getElementById('actionModal');
        if (modal) modal.classList.remove('open');
        state.pendingConfirm = null;
      }

      // Input listener for factory.reset 'RESET' verification
      const resetInputEl = document.getElementById('modalResetConfirmInput');
      if (resetInputEl) {
        resetInputEl.addEventListener('input', () => {
          const btn = document.getElementById('modalConfirmBtn');
          if (state.pendingConfirm && state.pendingConfirm.action === 'factory.reset') {
            const match = resetInputEl.value.trim() === 'RESET';
            if (btn) btn.disabled = !match;
            state.pendingConfirm.payload.confirmText = resetInputEl.value.trim();
          }
        });
      }

      // -----------------------------------------------------------------------
      // CUSTOM SELECT ENHANCER
      // 原生 <select> 的弹出层由系统绘制，CSS 无法主题化；
      // 这里把它替换成自绘面板，仍然写回 select.value 并派发 change 事件，
      // 因此所有既有 [data-action-change] 逻辑保持不变。
      // -----------------------------------------------------------------------
      const hxSelectActive = { wrap: null };

      function closeAllHxSelects(except) {
        document.querySelectorAll('.hx-select.open').forEach(wrap => {
          if (wrap === except) return;
          wrap.classList.remove('open');
          if (wrap.__hxMenu) wrap.__hxMenu.classList.remove('is-open');
        });
        if (!except) hxSelectActive.wrap = null;
      }

      function positionHxSelectMenu(wrap) {
        const trigger = wrap.querySelector('.hx-select-trigger');
        const menu = wrap.__hxMenu;
        if (!trigger || !menu) return;
        const r = trigger.getBoundingClientRect();
        const vh = window.innerHeight;
        const below = vh - r.bottom - 14;
        const above = r.top - 14;
        const openUp = below < 190 && above > below;
        menu.style.left = Math.round(r.left) + 'px';
        menu.style.width = Math.round(r.width) + 'px';
        if (openUp) {
          menu.style.top = 'auto';
          menu.style.bottom = Math.round(vh - r.top + 6) + 'px';
          menu.style.maxHeight = Math.max(130, Math.min(268, above)) + 'px';
        } else {
          menu.style.bottom = 'auto';
          menu.style.top = Math.round(r.bottom + 6) + 'px';
          menu.style.maxHeight = Math.max(130, Math.min(268, below)) + 'px';
        }
      }

      function buildHxSelectMenu(wrap) {
        const sel = wrap.querySelector('select');
        const menu = wrap.__hxMenu;
        const trigger = wrap.querySelector('.hx-select-trigger');
        if (!sel || !menu || !trigger) return;
        const frag = document.createDocumentFragment();
        const opts = Array.prototype.slice.call(sel.options);
        if (!opts.length) {
          const empty = document.createElement('div');
          empty.className = 'hx-select-empty';
          empty.textContent = '暂无可选项';
          frag.appendChild(empty);
        } else {
          opts.forEach((opt, i) => {
            const item = document.createElement('div');
            item.className = 'hx-select-option' + (i === sel.selectedIndex ? ' is-selected' : '');
            item.textContent = opt.textContent;
            item.dataset.hxIndex = String(i);
            frag.appendChild(item);
          });
        }
        menu.innerHTML = '';
        menu.appendChild(frag);

        const cur = sel.options[sel.selectedIndex];
        const valEl = trigger.querySelector('.hx-select-value');
        if (valEl) valEl.textContent = cur ? cur.textContent : '—';
      }

      function openHxSelect(wrap) {
        closeAllHxSelects(wrap);
        buildHxSelectMenu(wrap);
        if (wrap.__hxMenu) wrap.__hxMenu.classList.add('is-open');
        wrap.classList.add('open');
        positionHxSelectMenu(wrap);
        hxSelectActive.wrap = wrap;
        const cur = wrap.__hxMenu && wrap.__hxMenu.querySelector('.is-selected');
        if (cur && cur.scrollIntoView) cur.scrollIntoView({ block: 'nearest' });
      }

      function enhanceHxSelect(sel) {
        if (!sel || sel.dataset.hxEnhanced === '1') return;
        sel.dataset.hxEnhanced = '1';

        const wrap = document.createElement('div');
        wrap.className = 'hx-select';
        /* 继承原 select 上的宽度约束，避免布局跳变 */
        if (sel.style.maxWidth) wrap.style.maxWidth = sel.style.maxWidth;
        if (sel.style.width && sel.style.width !== '100%') wrap.style.width = sel.style.width;

        sel.parentNode.insertBefore(wrap, sel);
        wrap.appendChild(sel);
        sel.classList.add('hx-select-native');

        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = 'hx-select-trigger';
        trigger.setAttribute('aria-haspopup', 'listbox');
        trigger.innerHTML = '<span class="hx-select-value"></span>' +
          '<svg class="hx-select-caret" viewBox="0 0 24 24" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>';
        wrap.appendChild(trigger);

        const menu = document.createElement('div');
        menu.className = 'hx-select-menu';
        menu.setAttribute('role', 'listbox');
        document.body.appendChild(menu);
        wrap.__hxMenu = menu;

        trigger.addEventListener('click', (e) => {
          e.preventDefault();
          e.stopPropagation();
          if (wrap.classList.contains('open')) closeAllHxSelects();
          else openHxSelect(wrap);
        });

        menu.addEventListener('click', (e) => {
          const item = e.target.closest('.hx-select-option');
          if (!item) return;
          const idx = Number(item.dataset.hxIndex);
          if (Number.isNaN(idx)) return;
          sel.selectedIndex = idx;
          buildHxSelectMenu(wrap);
          closeAllHxSelects();
          sel.dispatchEvent(new Event('change', { bubbles: true }));
        });

        /* 脚本重建 <option> 时同步面板与显示值 */
        const mo = new MutationObserver(() => buildHxSelectMenu(wrap));
        mo.observe(sel, { childList: true, subtree: true, characterData: true });

        buildHxSelectMenu(wrap);
      }

      function enhanceAllHxSelects() {
        document.querySelectorAll('select.select-input').forEach(enhanceHxSelect);
      }

      document.addEventListener('click', (e) => {
        if (e.target.closest('.hx-select') || e.target.closest('.hx-select-menu')) return;
        closeAllHxSelects();
      }, true);
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') closeAllHxSelects();
      });
      window.addEventListener('scroll', (e) => {
        /* 下拉面板自身的滚动不能触发关闭 */
        const t = e.target;
        if (t && t.nodeType === 1 && t.closest && t.closest('.hx-select-menu')) return;
        closeAllHxSelects();
      }, true);
      window.addEventListener('resize', () => closeAllHxSelects());
      window.addEventListener('blur', () => closeAllHxSelects());

      enhanceAllHxSelects();
      window.__enhanceHxSelects = enhanceAllHxSelects;

      // Global Delegated Click Handler for [data-action]
      document.addEventListener('click', (e) => {
        const target = e.target.closest('[data-action]');
        if (!target || target.disabled) return;
        const action = target.dataset.action;

        switch (action) {
          case 'nav.switch':
            if (target.dataset.page) switchPage(target.dataset.page);
            /* 进入日志页时主动拉一次：宿主不推 logs.append，
               不查的话日志页永远是空的。 */
            if (target.dataset.page === 'logs') {
              sendCmd('logs.query', { offset: 0, limit: 200, level: state.logsFilterLevel }, target);
            }
            break;
          case 'ui.toggleTheme': {
            const nextTheme = state.theme === 'dark' ? 'light' : 'dark';
            applyThemeVisual(nextTheme);
            sendCmd('theme.set', { theme: nextTheme });
            break;
          }
          case 'theme.set': {
            const t = target.dataset.theme === 'light' ? 'light' : 'dark';
            applyThemeVisual(t);
            sendCmd('theme.set', { theme: t });
            break;
          }
          case 'ui.togglePerfLow':
            togglePerfLowMode();
            break;
          case 'ui.toggle3dExplode': {
            const card = document.getElementById('gpu3dCard');
            const stage = document.getElementById('gpu3dStage');
            const banner = document.getElementById('deviceHeroBanner');
            const lbl = document.getElementById('btnHeroExplodeLabel');
            if (card) {
              const isExp = card.classList.toggle('exploded');
              if (stage) stage.classList.toggle('is-exploded', isExp);
              if (banner) banner.classList.toggle('hero-exploded', isExp);
              if (lbl) lbl.textContent = isExp ? '合拢显卡模型' : '拆解透视图';
              showToast(isExp ? '已展开 6 大核心模组工程拆解图（支持鼠标 3D 追踪环视）' : '已合拢 3D 显卡标准结构', 'info');
            }
            break;
          }
          case 'ui.toggleSafetyCover': {
            const cover = document.getElementById('safetyCover');
            state.safetyCoverOpen = !state.safetyCoverOpen;
            if (cover) {
              cover.classList.toggle('open', state.safetyCoverOpen);
              const holder = cover.closest('.safety-cover-container');
              if (holder) holder.classList.toggle('armed', state.safetyCoverOpen);
            }
            showToast(state.safetyCoverOpen ? '机械安全盖已掀开，可执行引导操作' : '机械安全盖已闭合', 'warn');
            break;
          }
          case 'unlock.selectCard':
            state.selectedUnlockCard = target.dataset.card || '90HX';
            renderUnlockPage();
            break;
          case 'unlock.selectChannel':
            if (target.classList.contains('disabled')) return;
            state.selectedUnlockChannel = target.dataset.channel || 'A';
            renderUnlockPage();
            break;
          case 'modal.confirm':
            openDangerConfirmModal(target.dataset.confirmType, target);
            break;
          case 'modal.close':
            closeDangerModal();
            break;
          case 'modal.execute':
            if (state.pendingConfirm) {
              const pc = state.pendingConfirm;
              if (pc.action === 'factory.reset' && pc.payload.confirmText !== 'RESET') {
                showToast('请手动输入大写 RESET 后再确认', 'error');
                return;
              }
              closeDangerModal();
              sendCmd(pc.action, pc.payload, pc.triggerBtn);
            }
            break;
          case 'device.refresh':
          case 'unlock.scan':
          case 'power.disable':
            // 这些各自发自己的 action，不能共用一个 sendCmd
            sendCmd(action, {}, target);
            break;
          case 'dual.scanGames': {
            // 只有扫描游戏需要带盘符参数
            const driveSel = document.getElementById('dualScanDrive');
            sendCmd('dual.scanGames', { drive: driveSel ? driveSel.value : '' }, target);
            break;
          }
          case 'overlay.unlock':
          case 'overlay.cleanCache':
            // 这两个必须单独走自己的 action，不能落到上面的个案里
            sendCmd(action, {}, target);
            break;
          case 'logs.export':
          case 'logs.clear':
          case 'update.check':
          case 'update.download':
          case 'update.apply':
          case 'window.min':
          case 'window.max':
          case 'window.close':
            if (action === 'window.min') {
              isWindowMinimized = !isWindowMinimized;
            }
            sendCmd(action, {}, target);
            break;
          case 'spoof.restore':
            sendCmd('spoof.restore', { gpuSlot: Number(state.selectedSpoofSlot) || 0 }, target);
            break;
          case 'power.enable': {
            const sec = parseInt((document.getElementById('powerIdleSecondsInput') || {}).value, 10) || 8;
            sendCmd('power.enable', { idleSeconds: sec }, target);
            break;
          }
          case 'power.setPreset': {
            const preset = target.dataset.preset || 'adaptive';
            if (state.powerState) {
              state.powerState.preset = preset;
              renderPowerPage();
            }
            sendCmd('power.setPreset', { preset: preset });
            break;
          }
          case 'dlss.preflight':
            sendCmd('dlss.preflight', getDlssPreflightPayload(), target);
            break;
          case 'dlss.deploy': {
            const base = getDlssPreflightPayload();
            // LOCKED KEYS PER V2_REVIEW_VERDICT.md SECTION 6-#2: { optLevel, maxFrames, engine, preset, srMode }
            const payload = {
              gameDir: base.gameDir,
              proxyName: base.proxyName,
              injectReFramework: base.injectReFramework,
              options: {
                optLevel: (document.getElementById('dlssOptLevel') || {}).value || '1',
                maxFrames: (document.getElementById('dlssMaxFrames') || {}).value || '4',
                engine: (document.getElementById('dlssEngine') || {}).value || 'fsr3',
                preset: (document.getElementById('dlssPreset') || {}).value || 'C',
                srMode: (document.getElementById('dlssSrMode') || {}).value || 'quality'
              }
            };
            sendCmd('dlss.deploy', payload, target);
            break;
          }
          case 'dlss.checkUpdate': {
            const mIdx = parseInt((document.getElementById('dlssMirrorSelect') || {}).value, 10) || 0;
            sendCmd('dlss.checkUpdate', { mirrorIndex: mIdx }, target);
            break;
          }
          case 'dialog.browse': {
            const kind = target.dataset.browseKind || 'dir';
            state.lastBrowseTargetId = target.dataset.browseTarget || '';
            sendCmd('dialog.browse', { kind: kind }, target);
            break;
          }
          case 'dual.bind': {
            const exePath = ((document.getElementById('dualBindExeInput') || {}).value || '').trim();
            if (!exePath) {
              showToast('请先输入或选择要绑定的程序 .exe 路径', 'warn');
              return;
            }
            sendCmd('dual.bind', { exePath: exePath }, target);
            break;
          }
          case 'dual.unbind': {
            const exePath = target.dataset.exePath || '';
            sendCmd('dual.unbind', { exePath: exePath }, target);
            break;
          }
          case 'snapshot.create': {
            const labelInput = document.getElementById('snapshotLabelInput');
            const label = (labelInput && labelInput.value.trim()) ? labelInput.value.trim() : '手动创建系统快照';
            if (labelInput) labelInput.value = '';
            sendCmd('snapshot.create', { label: label }, target);
            break;
          }
          case 'snapshot.delete': {
            const snapId = target.dataset.snapshotId || '';
            sendCmd('snapshot.delete', { snapshotId: snapId }, target);
            break;
          }
          case 'snapshot.filterKind': {
            state.snapshotFilterKind = target.dataset.kind || 'all';
            target.parentElement.querySelectorAll('.seg-btn').forEach(b => {
              b.classList.toggle('active', b === target);
            });
            renderMaintenancePage();
            break;
          }
          case 'logs.filterLevel': {
            state.logsFilterLevel = target.dataset.level || 'all';
            const bar = document.getElementById('logsFilterBar');
            if (bar) {
              bar.querySelectorAll('.seg-btn').forEach(b => {
                b.classList.toggle('active', b.dataset.level === state.logsFilterLevel);
              });
            }
            renderLogsPage();
            break;
          }
          case 'logs.loadMore':
            sendCmd('logs.query', { offset: state.logsList.length, limit: 100, level: state.logsFilterLevel }, target);
            break;
          case 'logs.scrollToBottom': {
            state.logsAutoScroll = true;
            const resumeBtn = document.getElementById('btnLogsResumeScroll');
            if (resumeBtn) resumeBtn.style.display = 'none';
            const box = document.getElementById('logStreamBox');
            if (box) box.scrollTop = box.scrollHeight;
            break;
          }
          case 'logs.copy': {
            const text = state.logsList.map(i => '[' + i.at + '] [' + (i.level || 'info').toUpperCase() + '] ' + i.text).join('\n');
            if (navigator.clipboard && navigator.clipboard.writeText) {
              navigator.clipboard.writeText(text).then(() => showToast('日志已复制到剪贴板', 'success'));
            } else {
              showToast('已准备 ' + state.logsList.length + ' 条日志文本', 'info');
            }
            break;
          }
        }
      });

      // Delegated Change Handler for Select Elements
      document.addEventListener('change', (e) => {
        const target = e.target.closest('[data-action-change]');
        if (!target) return;
        const act = target.dataset.actionChange;
        if (act === 'spoof.selectSlot') {
          state.selectedSpoofSlot = parseInt(target.value, 10) || 0;
          renderSpoofPage();
        } else if (act === 'spoof.selectModel') {
          state.selectedSpoofTarget = target.value;
          renderSpoofPage();
        } else if (act === 'dlss.changeMaxFrames') {
          const mf = target.value || '4';
          const genCount = Math.max(1, (parseInt(mf, 10) || 4) - 1);
          const badge = document.getElementById('dlssPipelineBadge');
          const sub = document.getElementById('dlssPipelineModeSub');
          if (badge) badge.textContent = mf + 'X 多帧生成管线';
          if (sub) sub.textContent = '当前倍率: ' + mf + 'X (每帧生成 ' + genCount + ' 帧)';
        }
      });

      // Logs search & scroll listener (stop auto-scroll when user scrolls up)
      const logsSearchEl = document.getElementById('logsSearchInput');
      if (logsSearchEl) {
        logsSearchEl.addEventListener('input', () => {
          state.logsSearchKeyword = logsSearchEl.value;
          renderLogsPage();
        });
      }
      const logBoxEl = document.getElementById('logStreamBox');
      if (logBoxEl) {
        logBoxEl.addEventListener('scroll', () => {
          const nearBottom = (logBoxEl.scrollHeight - logBoxEl.scrollTop - logBoxEl.clientHeight) < 28;
          state.logsAutoScroll = nearBottom;
          const resumeBtn = document.getElementById('btnLogsResumeScroll');
          if (resumeBtn) resumeBtn.style.display = nearBottom ? 'none' : 'inline-flex';
          if (logBoxEl.scrollTop === 0 && state.logsList.length >= 10) {
            sendCmd('logs.query', { offset: state.logsList.length, limit: 100, level: state.logsFilterLevel });
          }
        });
      }

      // Keyboard Shortcuts (Section 6.4: Esc closes modal, Ctrl+F focuses log search, Enter on role="button")
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
          closeDangerModal();
        } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'f') {
          if (state.currentPage === 'logs') {
            e.preventDefault();
            const searchInput = document.getElementById('logsSearchInput');
            if (searchInput) searchInput.focus();
          }
        } else if (e.key === 'Enter' || e.key === ' ') {
          const active = document.activeElement;
          if (active && active.getAttribute('role') === 'button' && active.dataset.action) {
            e.preventDefault();
            active.click();
          }
        }
      });

      // Visibility state listener (Section 7 requirement: pause rAF when hidden)
      document.addEventListener('visibilitychange', () => {
        isPageHidden = document.visibilityState === 'hidden';
      });

      // 3D GPU Card Continuous 16s Orbit (-24deg..-6deg, 12deg..2deg) — no inline transform jump on explode
      (function init3dTilt() {
        const stage = document.getElementById('gpu3dStage');
        const card = document.getElementById('gpu3dCard');
        const glare = document.getElementById('gpuMouseGlare');
        if (!stage || !card) return;

        let isHovering = false;
        let mouseNX = 0, mouseNY = 0;
        let curRY = -18, curRX = 8, curRZ = -1, curScale = 1, curLiftY = 0;
        let orbitPhase = -0.75;
        let lastTs = performance.now();
        /* 性能守卫：帧率采样 + 失焦暂停标记 */
        let perfFrames = 0;
        let perfAccumMs = 0;
        let windowBlurred = false;
        window.addEventListener('blur', () => { windowBlurred = true; });
        window.addEventListener('focus', () => { windowBlurred = false; });

        stage.addEventListener('mouseenter', () => {
          if (isPerfLow) return;
          isHovering = true;
        });

        stage.addEventListener('mousemove', (e) => {
          if (isPerfLow) return;
          isHovering = true;
          const rect = stage.getBoundingClientRect();
          const px = Math.max(0, Math.min(1, (e.clientX - rect.left) / Math.max(1, rect.width)));
          const py = Math.max(0, Math.min(1, (e.clientY - rect.top) / Math.max(1, rect.height)));
          mouseNX = (px - 0.5) * 2;
          mouseNY = (py - 0.5) * 2;
          if (glare) {
            glare.style.setProperty('--glare-x', (px * 100).toFixed(1) + '%');
            glare.style.setProperty('--glare-y', (py * 100).toFixed(1) + '%');
          }
        });

        stage.addEventListener('mouseleave', () => {
          isHovering = false;
          const ratio = Math.max(-1, Math.min(1, curRY / 24));
          orbitPhase = Math.asin(ratio);
        });

        function tick3d(now) {
          const rawDt = now - lastTs;
          const dt = Math.min(0.05, Math.max(0.001, rawDt / 1000));
          lastTs = now;

          // 帧率守卫：每 2 秒统计一次，平均帧耗时 >22ms（约 <45fps）自动降级到 perf-low
          perfFrames++;
          perfAccumMs += rawDt;
          if (perfAccumMs > 2000) {
            const avgMs = perfAccumMs / Math.max(1, perfFrames);
            if (avgMs > 22 && !isPerfLow) {
              isPerfLow = true;
              document.body.classList.add('perf-low');
              try { showToast('检测到渲染压力较大，已自动切换到低功耗动效', 'info'); } catch (e) {}
            }
            perfAccumMs = 0;
            perfFrames = 0;
          }

          // 高刷屏节流：低于 13ms（约 75fps 以上）的帧不重写 transform，
          // 慢速环绕动画肉眼看不出差别，但合成开销减半
          if (rawDt < 13) { requestAnimationFrame(tick3d); return; }

          if (!isPageHidden && !isWindowMinimized && !isPerfLow && !windowBlurred && state.currentPage === 'device') {
            let targetRY, targetRX, targetRZ, targetScale, targetLiftY;

            if (isHovering) {
              targetRY = mouseNX * 30;
              targetRX = -mouseNY * 22 + 4;
              targetRZ = mouseNX * -2.0;
              targetScale = 1.08;
              targetLiftY = -4;
            } else {
              orbitPhase += dt * 1.05;
              targetRY = Math.sin(orbitPhase) * 24;
              targetRX = 7 + Math.cos(orbitPhase * 0.6) * 5.5;
              targetRZ = Math.sin(orbitPhase) * -1.4;
              targetScale = 1.02;
              targetLiftY = -1;
            }

            const damping = isHovering ? 0.14 : 0.08;
            curRY += (targetRY - curRY) * damping;
            curRX += (targetRX - curRX) * damping;
            curRZ += (targetRZ - curRZ) * damping;
            curScale += (targetScale - curScale) * damping;
            curLiftY += (targetLiftY - curLiftY) * damping;

            card.style.transform =
              'translate3d(0, ' + curLiftY.toFixed(2) + 'px, 0) ' +
              'scale(' + curScale.toFixed(3) + ') ' +
              'rotateX(' + curRX.toFixed(2) + 'deg) ' +
              'rotateY(' + curRY.toFixed(2) + 'deg) ' +
              'rotateZ(' + curRZ.toFixed(2) + 'deg)';
          }
          requestAnimationFrame(tick3d);
        }
        requestAnimationFrame(tick3d);
      })();

      // Auto-Running Multi-Mode Power Reactor & Spectrum Waveform Engine (Sleep / Adaptive / Turbo)
      (function initCanvasEngines() {
        const ambientCanvas = document.getElementById('ambientParticleCanvas');
        const fireCanvas = document.getElementById('turboFireCanvas');
        const waveCanvas = document.getElementById('powerWaveCanvas');
        const powerStage = document.getElementById('powerAnimStage');
        if (!ambientCanvas || !fireCanvas || !waveCanvas) return;

        const aCtx = ambientCanvas.getContext('2d');
        const fCtx = fireCanvas.getContext('2d');
        const wCtx = waveCanvas.getContext('2d');

        let aw = (ambientCanvas.width = window.innerWidth);
        let ah = (ambientCanvas.height = window.innerHeight);

        const particles = Array.from({ length: 28 }, () => ({
          x: Math.random() * aw,
          y: Math.random() * ah,
          r: Math.random() * 1.5 + 0.6
        }));

        function drawStaticAmbient() {
          if (isPerfLow) return;
          const styles = getComputedStyle(document.body);
          const accentRgb = (styles.getPropertyValue('--accent-rgb') || '56, 189, 248').trim();
          aCtx.clearRect(0, 0, aw, ah);
          aCtx.fillStyle = 'rgba(' + accentRgb + ', 0.28)';
          for (let i = 0; i < particles.length; i++) {
            const p = particles[i];
            aCtx.beginPath();
            aCtx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
            aCtx.fill();
          }
        }

        // Particle pools for the 3 distinct Power Presets on #turboFireCanvas
        const turboEmbers = Array.from({ length: 68 }, () => ({
          x: Math.random() * 860,
          y: Math.random() * 160,
          vx: (Math.random() - 0.35) * 2.8,
          vy: -(Math.random() * 2.6 + 1.2),
          size: Math.random() * 3.4 + 1.2,
          life: Math.random(),
          hue: Math.floor(Math.random() * 42) // 0=crimson, 20=orange, 40=gold
        }));

        const adaptiveNodes = Array.from({ length: 32 }, (_, i) => ({
          x: (i / 32) * 860,
          y: 25 + Math.random() * 110,
          vx: Math.random() * 1.6 + 0.8,
          r: Math.random() * 2.0 + 1.0
        }));

        const sleepMotes = Array.from({ length: 22 }, () => ({
          x: Math.random() * 860,
          y: Math.random() * 160,
          vy: -(Math.random() * 0.35 + 0.12),
          r: Math.random() * 2.2 + 0.8,
          phase: Math.random() * Math.PI * 2
        }));

        let wavePhase = 0;
        let shockwaveRadius = 12;
        let powerRaf = null;
        /* 换挡交叉淡化状态：捕获上一模式最后一帧，作为残影淡出 */
        let lastPresetDrawn = null;
        let presetTransition = { from: null, fromWave: null, start: 0 };
        const PRESET_FADE_MS = 520;
        /* 画布与卡片同尺寸（逃逸效果已回退，保留常量便于后续调整） */
        const FIRE_PAD = 0;
        let fireLogicalW = 0;
        let fireLogicalH = 0;

        function syncFireCanvasSize() {
          if (!powerStage) return;
          const rect = powerStage.getBoundingClientRect();
          /* fw/fh 仍是"卡片内容区"的逻辑尺寸，绘制坐标不用改；
             实际画布 buffer 四周各多 FIRE_PAD，画在外面的粒子不会被切掉。
             不用 max() 夹取，保证 buffer 与 CSS 尺寸 1:1，溢出部分才不失真 */
          const lw = Math.round(rect.width) || 780;
          const lh = Math.round(rect.height) || 156;
          fireLogicalW = lw;
          fireLogicalH = lh;
          const bw = lw + FIRE_PAD * 2;
          const bh = lh + FIRE_PAD * 2;
          if (fireCanvas.width !== bw) fireCanvas.width = bw;
          if (fireCanvas.height !== bh) fireCanvas.height = bh;
        }

        function drawPowerWaveFrame() {
          syncFireCanvasSize();
          const preset = (state.powerState && state.powerState.preset) || 'adaptive';
          const fw = fireCanvas.width;
          const fh = fireCanvas.height;
          const ww = waveCanvas.width;
          const wh = waveCanvas.height;

          /* 模式切换：先把上一模式的最后一帧抓下来，稍后叠在新画面上淡出 */
          if (lastPresetDrawn && lastPresetDrawn !== preset) {
            try {
              const offF = document.createElement('canvas');
              offF.width = fw;
              offF.height = fh;
              offF.getContext('2d').drawImage(fireCanvas, 0, 0);
              const offW = document.createElement('canvas');
              offW.width = ww;
              offW.height = wh;
              offW.getContext('2d').drawImage(waveCanvas, 0, 0);
              presetTransition.from = offF;
              presetTransition.fromWave = offW;
              presetTransition.start = performance.now();
            } catch (e) { /* 忽略过渡失败，保证动画继续 */ }
          }
          lastPresetDrawn = preset;

          /* 坐标系原点移到卡片左上角；-(FIRE_PAD) 之外的部分画在卡片外面 */
          fCtx.setTransform(1, 0, 0, 1, FIRE_PAD, FIRE_PAD);
          fCtx.clearRect(-FIRE_PAD, -FIRE_PAD, fw + FIRE_PAD * 2, fh + FIRE_PAD * 2);
          wCtx.clearRect(0, 0, ww, wh);

          const reactorCx = fw - 104;
          const reactorCy = fh * 0.5;

          // ===============================================================
          // MODE 1: TURBO ("满血狂暴") — Blazing Fire Embers, Shockwave Rings,
          // Plasma Lightning Arcs, 24-Bar Overdrive Spectrum & Triple Wave
          // ===============================================================
          if (preset === 'turbo') {
            wavePhase += 0.16;

            // 1A. Background radial heat aura around the reactor
            const aura = fCtx.createRadialGradient(reactorCx, reactorCy, 6, reactorCx, reactorCy, 190);
            aura.addColorStop(0, 'rgba(244, 63, 94, 0.42)');
            aura.addColorStop(0.45, 'rgba(249, 115, 22, 0.22)');
            aura.addColorStop(1, 'rgba(244, 63, 94, 0)');
            fCtx.fillStyle = aura;
            fCtx.fillRect(0, 0, fw, fh);

            // 1B. Expanding dual shockwave rings from reactor core
            shockwaveRadius = (shockwaveRadius + 2.4) % 150;
            for (let s = 0; s < 2; s++) {
              const r = (shockwaveRadius + s * 75) % 150;
              const alpha = Math.max(0, (1 - r / 150) * 0.55);
              fCtx.beginPath();
              fCtx.arc(reactorCx, reactorCy, 24 + r, 0, Math.PI * 2);
              fCtx.strokeStyle = s === 0 ? ('rgba(251, 191, 36, ' + alpha.toFixed(2) + ')') : ('rgba(244, 63, 94, ' + alpha.toFixed(2) + ')');
              fCtx.lineWidth = 2;
              fCtx.stroke();
            }

            // 1C. Crackling plasma lightning arcs around reactor core
            for (let a = 0; a < 4; a++) {
              const baseAngle = wavePhase * 0.9 + (a * Math.PI * 0.5);
              fCtx.beginPath();
              let lx = reactorCx + Math.cos(baseAngle) * 32;
              let ly = reactorCy + Math.sin(baseAngle) * 32;
              fCtx.moveTo(lx, ly);
              for (let seg = 1; seg <= 4; seg++) {
                const rad = 32 + seg * 11;
                const jitter = (Math.sin(wavePhase * 3.7 + a * 5 + seg * 2.3) * 0.24);
                lx = reactorCx + Math.cos(baseAngle + jitter) * rad;
                ly = reactorCy + Math.sin(baseAngle + jitter) * rad;
                fCtx.lineTo(lx, ly);
              }
              fCtx.strokeStyle = a % 2 === 0 ? 'rgba(253, 224, 71, 0.78)' : 'rgba(244, 63, 94, 0.82)';
              fCtx.lineWidth = 1.4;
              fCtx.stroke();
            }

            // 1D. 68 High-velocity rising flame & plasma embers across the stage
            for (let i = 0; i < turboEmbers.length; i++) {
              const e = turboEmbers[i];
              e.x += e.vx + Math.sin(wavePhase + i) * 0.65;
              e.y += e.vy;
              e.life -= 0.016;
              if (e.y < -8 || e.x < -10 || e.x > fw + 10 || e.life <= 0) {
                // Respawn near bottom or around reactor core
                if (i % 3 === 0) {
                  const ang = Math.random() * Math.PI * 2;
                  e.x = reactorCx + Math.cos(ang) * (22 + Math.random() * 35);
                  e.y = reactorCy + Math.sin(ang) * (22 + Math.random() * 35);
                  e.vx = Math.cos(ang) * (1.5 + Math.random() * 2.2);
                  e.vy = Math.sin(ang) * (1.5 + Math.random() * 2.2) - 0.8;
                } else {
                  e.x = Math.random() * fw;
                  e.y = fh + 4;
                  e.vx = (Math.random() - 0.3) * 2.4;
                  e.vy = -(Math.random() * 2.8 + 1.4);
                }
                e.life = 0.75 + Math.random() * 0.25;
              }

              // Ember trail
              fCtx.beginPath();
              fCtx.moveTo(e.x, e.y);
              fCtx.lineTo(e.x - e.vx * 2.6, e.y - e.vy * 2.6);
              fCtx.strokeStyle = 'rgba(249, 115, 22, ' + (e.life * 0.55).toFixed(2) + ')';
              fCtx.lineWidth = e.size * 0.7;
              fCtx.stroke();

              // Ember glowing head
              fCtx.beginPath();
              fCtx.arc(e.x, e.y, e.size, 0, Math.PI * 2);
              fCtx.fillStyle = e.hue > 26
                ? ('rgba(253, 224, 71, ' + (e.life * 0.92).toFixed(2) + ')')
                : (e.hue > 12
                  ? ('rgba(249, 115, 22, ' + (e.life * 0.88).toFixed(2) + ')')
                  : ('rgba(244, 63, 94, ' + (e.life * 0.88).toFixed(2) + ')'));
              fCtx.fill();
            }

            // 1E. Oscilloscope (#powerWaveCanvas): 24-Bar Full-Load Overdrive Spectrum + Triple Harmonic Wave
            const barCount = 24;
            const barGap = 3;
            const barW = (ww - (barCount - 1) * barGap) / barCount;
            const barGrad = wCtx.createLinearGradient(0, wh, 0, 4);
            barGrad.addColorStop(0, 'rgba(244, 63, 94, 0.35)');
            barGrad.addColorStop(0.55, 'rgba(249, 115, 22, 0.65)');
            barGrad.addColorStop(1, 'rgba(251, 191, 36, 0.92)');
            wCtx.fillStyle = barGrad;

            for (let b = 0; b < barCount; b++) {
              const surge = 0.72 + 0.26 * Math.abs(Math.sin(wavePhase * 1.3 + b * 0.45) * Math.cos(wavePhase * 0.8 - b * 0.25));
              const bh = Math.max(10, Math.min(wh - 6, surge * (wh - 8)));
              const bx = b * (barW + barGap);
              wCtx.fillRect(bx, wh - bh, barW, bh);
              // White-hot peak cap
              wCtx.fillStyle = 'rgba(254, 240, 138, 0.95)';
              wCtx.fillRect(bx, wh - bh - 2, barW, 2);
              wCtx.fillStyle = barGrad;
            }

            // High-frequency overdrive wave crest
            wCtx.beginPath();
            for (let x = 0; x <= ww; x += 3) {
              const y = wh * 0.38 +
                Math.sin(x * 0.045 + wavePhase * 2.2) * 11 +
                Math.cos(x * 0.09 - wavePhase * 3.1) * 5.5 +
                Math.sin(x * 0.18 + wavePhase * 4.5) * 2.5;
              if (x === 0) wCtx.moveTo(x, y);
              else wCtx.lineTo(x, y);
            }
            wCtx.strokeStyle = '#fef08a';
            wCtx.lineWidth = 2.2;
            wCtx.stroke();

          // ===============================================================
          // MODE 2: SLEEP ("极致静音") — Calm Emerald Cryo-Motes,
          // Low Eco Spectrum Bars & Smooth P8 Flatline Wave
          // ===============================================================
          } else if (preset === 'sleep') {
            wavePhase += 0.035;

            const cryoGlow = fCtx.createRadialGradient(reactorCx, reactorCy, 8, reactorCx, reactorCy, 150);
            cryoGlow.addColorStop(0, 'rgba(52, 211, 153, 0.22)');
            cryoGlow.addColorStop(1, 'rgba(52, 211, 153, 0)');
            fCtx.fillStyle = cryoGlow;
            fCtx.fillRect(0, 0, fw, fh);

            for (let i = 0; i < sleepMotes.length; i++) {
              const m = sleepMotes[i];
              m.y += m.vy;
              m.x += Math.sin(wavePhase + m.phase) * 0.25;
              if (m.y < -6) {
                m.y = fh + 6;
                m.x = Math.random() * fw;
              }
              fCtx.beginPath();
              fCtx.arc(m.x, m.y, m.r, 0, Math.PI * 2);
              fCtx.fillStyle = 'rgba(52, 211, 153, 0.42)';
              fCtx.fill();
            }

            // Low-power P8 eco bars (15% - 24% height)
            const barCount = 24;
            const barGap = 3;
            const barW = (ww - (barCount - 1) * barGap) / barCount;
            wCtx.fillStyle = 'rgba(52, 211, 153, 0.26)';
            for (let b = 0; b < barCount; b++) {
              const bh = 8 + Math.sin(wavePhase + b * 0.35) * 3.5;
              wCtx.fillRect(b * (barW + barGap), wh - bh, barW, bh);
            }

            // Smooth P8 baseline wave + area fill
            wCtx.beginPath();
            for (let x = 0; x <= ww; x += 4) {
              const y = wh * 0.68 + Math.sin(x * 0.02 + wavePhase) * 3.2;
              if (x === 0) wCtx.moveTo(x, y);
              else wCtx.lineTo(x, y);
            }
            wCtx.strokeStyle = '#34d399';
            wCtx.lineWidth = 2;
            wCtx.stroke();

          // ===============================================================
          // MODE 3: ADAPTIVE ("智能动态") — Cyber-Blue Data Stream Particles,
          // Dynamic Modulating Spectrum & Dual-Trace Harmonic Wave
          // ===============================================================
          } else {
            wavePhase += 0.065;

            const adaptGlow = fCtx.createRadialGradient(reactorCx, reactorCy, 8, reactorCx, reactorCy, 165);
            adaptGlow.addColorStop(0, 'rgba(56, 189, 248, 0.26)');
            adaptGlow.addColorStop(1, 'rgba(56, 189, 248, 0)');
            fCtx.fillStyle = adaptGlow;
            fCtx.fillRect(0, 0, fw, fh);

            for (let i = 0; i < adaptiveNodes.length; i++) {
              const n = adaptiveNodes[i];
              n.x += n.vx;
              if (n.x > fw + 12) {
                n.x = -12;
                n.y = 20 + Math.random() * (fh - 40);
              }
              fCtx.beginPath();
              fCtx.moveTo(n.x, n.y);
              fCtx.lineTo(n.x - n.vx * 8, n.y);
              fCtx.strokeStyle = 'rgba(56, 189, 248, 0.35)';
              fCtx.lineWidth = 1.2;
              fCtx.stroke();

              fCtx.beginPath();
              fCtx.arc(n.x, n.y, n.r, 0, Math.PI * 2);
              fCtx.fillStyle = 'rgba(56, 189, 248, 0.72)';
              fCtx.fill();
            }

            // Responsive adaptive spectrum bars
            const barCount = 24;
            const barGap = 3;
            const barW = (ww - (barCount - 1) * barGap) / barCount;
            wCtx.fillStyle = 'rgba(56, 189, 248, 0.32)';
            for (let b = 0; b < barCount; b++) {
              const ratio = 0.28 + 0.38 * Math.pow(Math.sin(wavePhase * 0.75 + b * 0.28) * 0.5 + 0.5, 1.6);
              const bh = Math.max(8, ratio * (wh - 10));
              wCtx.fillRect(b * (barW + barGap), wh - bh, barW, bh);
            }

            // Secondary harmonic trace (Indigo)
            wCtx.beginPath();
            for (let x = 0; x <= ww; x += 4) {
              const y = wh * 0.54 + Math.cos(x * 0.024 - wavePhase * 0.9) * 7.5;
              if (x === 0) wCtx.moveTo(x, y);
              else wCtx.lineTo(x, y);
            }
            wCtx.strokeStyle = 'rgba(129, 140, 248, 0.68)';
            wCtx.lineWidth = 1.5;
            wCtx.stroke();

            // Primary harmonic trace (Cyan)
            wCtx.beginPath();
            for (let x = 0; x <= ww; x += 4) {
              const y = wh * 0.50 + Math.sin(x * 0.028 + wavePhase * 1.2) * 8.5 + Math.cos(x * 0.055 - wavePhase) * 3.2;
              if (x === 0) wCtx.moveTo(x, y);
              else wCtx.lineTo(x, y);
            }
            wCtx.strokeStyle = '#38bdf8';
            wCtx.lineWidth = 2;
            wCtx.stroke();
          }

          /* 换挡残影淡出：旧模式的画面透明度线性衰减，与新画面自然融合 */
          if (presetTransition.from && presetTransition.start) {
            const k = (performance.now() - presetTransition.start) / PRESET_FADE_MS;
            if (k >= 1) {
              presetTransition.from = null;
              presetTransition.fromWave = null;
              presetTransition.start = 0;
            } else {
              const a = 1 - k;
              fCtx.save();
              fCtx.setTransform(1, 0, 0, 1, 0, 0);
              fCtx.globalAlpha = a * a;
              fCtx.drawImage(presetTransition.from, 0, 0);
              fCtx.restore();
              if (presetTransition.fromWave) {
                wCtx.save();
                wCtx.globalAlpha = a * a;
                wCtx.drawImage(presetTransition.fromWave, 0, 0);
                wCtx.restore();
              }
            }
          }

          if (!isPerfLow && !isPageHidden && !isWindowMinimized && state.currentPage === 'power') {
            powerRaf = requestAnimationFrame(drawPowerWaveFrame);
          } else {
            powerRaf = null;
          }
        }

        window.__ensurePowerAnimLoop = function() {
          if (!powerRaf && !isPerfLow && !isPageHidden && !isWindowMinimized) {
            powerRaf = requestAnimationFrame(drawPowerWaveFrame);
          }
        };

        window.addEventListener('resize', () => {
          aw = ambientCanvas.width = window.innerWidth;
          ah = ambientCanvas.height = window.innerHeight;
          drawStaticAmbient();
          syncFireCanvasSize();
        });

        drawStaticAmbient();
        setTimeout(() => {
          drawPowerWaveFrame();
        }, 120);
      })();

      /* =======================================================================
         BOOT & HOST INITIALIZATION (800MS TIMEOUT FALLBACK PER SECTION 4.6)
         ======================================================================= */
      if (hasHost) {
        window.chrome.webview.addEventListener('message', (e) => {
          handleIncomingEnvelope(e.data);
        });
      }

      renderDevicePage();
      renderUnlockPage();
      renderSpoofPage();
      renderPowerPage();
      renderDlssPage();
      renderOverlayPage();
      renderDualPage();
      renderMaintenancePage();
      renderLogsPage();
      renderUpdatePage();

      setTimeout(() => {
        sendCmd('app.init', {});
        const firstPaintMs = Math.round(performance.now() - bootTimestamp);
        sendCmd('app.ready', { ms: firstPaintMs });
        if (!receivedAnyEvt) {
          usingMock = true;
          bootMockDataPipe();
        }
      }, hasHost ? 800 : 20);

      /* =======================================================================
         =======================================================================
         演示数据，仅浏览器预览用 (MOCK DATA - FOR BROWSER PREVIEW ONLY)
         删除下方 MOCK 对象后，页面自动保持空态/加载态，绝不白屏或报错。
         =======================================================================
         ======================================================================= */
      var MOCK = {
        theme: 'light',
        deviceSnapshot: {
          gpu: {
            name: 'NVIDIA CMP 90HX',
            model: 'CMP 90HX',
            chip: 'GA102',
            deviceId: '10DE:220D',
            vram: '10GB GDDR6X',
            driverVersion: '616.92',
            bus: '0000:01:00.0',
            pcieGen: '2',
            pcieWidth: '16',
            pcieFormatted: 'PCIe x16 2.0 (全速已解锁)',
            isSpoofed: false,
            displayName: 'NVIDIA CMP 90HX'
          },
          sensors: {
            valid: true,
            temp: 62,
            fanPercent: 55,
            powerDraw: 148.5,
            powerLimit: 300,
            powerMinLimit: 100,
            powerDefaultLimit: 250,
            powerMaxLimit: 320,
            memUsedMB: 1024,
            memTotalMB: 10240,
            utilPercent: 18,
            coreClock: 1710,
            coreClockMax: 2100,
            memClock: 9501,
            memClockMax: 9501,
            pstate: 'P0',
            tempSlowdownC: 95,
            tempShutdownC: 98
          },
          system: {
            cpuName: 'AMD Ryzen 9 5900X',
            cpuCores: '12 核 24 线程',
            cpuClock: '3.70 GHz',
            motherboard: 'B550M',
            bios: '1.2.0.7',
            memory: '32 GB',
            os: 'Windows 11 26100',
            uefi: true,
            secureBoot: false,
            hags: true,
            uptime: '2 天 3 小时'
          },
          flags: {
            isAdmin: true,
            nvAppInstalled: true,
            isPowerSaverRunning: true
          }
        },
        gpuList: {
          gpus: [
            {
              slot: 0,
              detectedModel: 'CMP 90HX',
              chip: 'GA102',
              vram: '10GB GDDR6X',
              currentDesc: 'NVIDIA CMP 90HX',
              targetName: 'NVIDIA GeForce RTX 3080',
              isSpoofed: false,
              isComputeUnlocked: true,
              isPcieUnlocked: true
            }
          ]
        },
        unlockState: {
          mode: 'Mode1_DirectBoot',
          modeLabel: '模式 1 · UEFI 直启',
          detail: '已注入 NVPermissiveCompat.efi 到 ESP',
          hasBackup: true,
          isUefi: true,
          secureBoot: false,
          espMounted: true,
          channels: [
            { id: 'A', label: 'UEFI 直启', available: true, installed: true },
            { id: 'B', label: 'UEFI Shell 链启', available: true, installed: false },
            { id: 'C', label: 'U 盘引导', available: true, installed: false },
            { id: 'D', label: '恢复官方引导', available: true, installed: false }
          ],
          usbDrives: [
            { letter: 'E:\\', label: 'Kingston 32GB' },
            { letter: 'F:\\', label: 'SanDisk EFI Boot 16GB' }
          ]
        },
        spoofModels: {
          groups: [
            {
              chip: 'GA102',
              label: 'GA102（90HX / RTX 30 系 · 14 款）',
              models: [
                'NVIDIA GeForce RTX 3090 Ti', 'NVIDIA GeForce RTX 3090', 'NVIDIA GeForce RTX 3080 Ti',
                'NVIDIA GeForce RTX 3080', 'NVIDIA GeForce RTX 3080 12GB', 'NVIDIA RTX A6000',
                'NVIDIA RTX A5000', 'NVIDIA RTX A4500', 'NVIDIA A40', 'NVIDIA A10',
                'NVIDIA A10G', 'NVIDIA CMP 90HX', 'NVIDIA Tesla T40', 'NVIDIA Quadro RTX 8000 (Compat)'
              ]
            },
            {
              chip: 'GA104',
              label: 'GA104（70HX / RTX 30 系 · 14 款）',
              models: [
                'NVIDIA GeForce RTX 3070 Ti', 'NVIDIA GeForce RTX 3070', 'NVIDIA GeForce RTX 3060 Ti',
                'NVIDIA GeForce RTX 3080 Laptop GPU', 'NVIDIA GeForce RTX 3070 Ti Laptop GPU',
                'NVIDIA GeForce RTX 3070 Laptop GPU', 'NVIDIA RTX A4000', 'NVIDIA RTX A3000',
                'NVIDIA RTX A2000 12GB', 'NVIDIA A16', 'NVIDIA A2', 'NVIDIA CMP 70HX',
                'NVIDIA GeForce RTX 3060 GA104', 'NVIDIA RTX 4000 SFF Ada (Compat)'
              ]
            },
            {
              chip: 'TU102',
              label: 'TU102（50HX / RTX 20 系 · 13 款）',
              models: [
                'NVIDIA GeForce RTX 2080 Ti', 'NVIDIA TITAN RTX', 'NVIDIA Quadro RTX 8000',
                'NVIDIA Quadro RTX 6000', 'NVIDIA GeForce RTX 2080 Ti SUPER (Engineering)',
                'NVIDIA Tesla T40 (TU102)', 'NVIDIA GRID RTX 8000', 'NVIDIA GRID RTX 6000',
                'NVIDIA CMP 50HX', 'NVIDIA Quadro RTX 6000 Passive', 'NVIDIA Quadro RTX 8000 Passive',
                'NVIDIA GeForce RTX 2080 Ti Rev.A', 'NVIDIA TITAN RTX Passive'
              ]
            },
            {
              chip: 'TU106',
              label: 'TU106（40HX / RTX 20 系 · 14 款）',
              models: [
                'NVIDIA GeForce RTX 2070 SUPER', 'NVIDIA GeForce RTX 2070', 'NVIDIA GeForce RTX 2060 SUPER',
                'NVIDIA GeForce RTX 2060 12GB', 'NVIDIA GeForce RTX 2060', 'NVIDIA GeForce RTX 2070 Max-Q',
                'NVIDIA GeForce RTX 2060 Mobile', 'NVIDIA Quadro RTX 4000', 'NVIDIA Quadro RTX 3000',
                'NVIDIA Tesla T4', 'NVIDIA CMP 40HX', 'NVIDIA GeForce RTX 2060 KO',
                'NVIDIA Quadro RTX 4000 Max-Q', 'NVIDIA GRID T4-8Q'
              ]
            },
            {
              chip: 'TU116',
              label: 'TU116（30HX / GTX 16 系 · 12 款）',
              models: [
                'NVIDIA GeForce GTX 1660 Ti', 'NVIDIA GeForce GTX 1660 SUPER', 'NVIDIA GeForce GTX 1660',
                'NVIDIA GeForce GTX 1650 SUPER', 'NVIDIA GeForce GTX 1650 Ti', 'NVIDIA GeForce GTX 1650 (TU116)',
                'NVIDIA GeForce GTX 1660 Ti Mobile', 'NVIDIA GeForce GTX 1660 Ti Max-Q',
                'NVIDIA CMP 30HX', 'NVIDIA T1000 8GB', 'NVIDIA T600', 'NVIDIA Quadro T1000'
              ]
            },
            {
              chip: 'GA100',
              label: 'GA100（170HX / 数据中心计算卡 · 9 款）',
              models: [
                'NVIDIA A100-PCIE-40GB', 'NVIDIA A100-PCIE-80GB', 'NVIDIA A100-SXM4-40GB',
                'NVIDIA A100-SXM4-80GB', 'NVIDIA A100X', 'NVIDIA A30',
                'NVIDIA CMP 170HX', 'NVIDIACMP 170HX 8GB', 'NVIDIA HGX A100'
              ]
            },
            {
              chip: 'AD102',
              label: 'Ada（RTX 40 系 · 10 款）',
              models: [
                'NVIDIA GeForce RTX 4090', 'NVIDIA GeForce RTX 4080 SUPER', 'NVIDIA GeForce RTX 4080',
                'NVIDIA GeForce RTX 4070 Ti SUPER', 'NVIDIA GeForce RTX 4070 Ti', 'NVIDIA GeForce RTX 4070 SUPER',
                'NVIDIA GeForce RTX 4070', 'NVIDIA GeForce RTX 4060 Ti', 'NVIDIA GeForce RTX 4060 Ti 16GB',
                'NVIDIA GeForce RTX 4060'
              ]
            },
            {
              chip: 'GB202',
              label: 'Blackwell（RTX 50 系 · 6 款）',
              models: [
                'NVIDIA GeForce RTX 5090', 'NVIDIA GeForce RTX 5080', 'NVIDIA GeForce RTX 5070 Ti',
                'NVIDIA GeForce RTX 5070', 'NVIDIA GeForce RTX 5060 Ti', 'NVIDIA GeForce RTX 5060'
              ]
            }
          ],
          recommended: 'NVIDIA GeForce RTX 3080'
        },
        spoofState: {
          gpus: [
            {
              slot: 0,
              detectedModel: 'CMP 90HX',
              chip: 'GA102',
              currentDesc: 'NVIDIA CMP 90HX',
              isSpoofed: false,
              targetName: 'NVIDIA GeForce RTX 3080'
            }
          ]
        },
        powerState: {
          enabled: true,
          running: true,
          preset: 'adaptive',
          idleSeconds: 8,
          currentPstate: 'P8',
          powerRange: { min: 100, default: 250, max: 320, current: 250 },
          taskInstalled: true,
          lastTrigger: '2026-10-20 17:22:31'
        },
        dlssState: {
          localVersion: '0.4.0',
          latestVersion: '0.4.2',
          hasUpdate: true,
          runtimeFound: true,
          runtimePath: 'C:\\ProgramData\\CMP90HX_Toolbox\\dlss_runtime',
          mirrors: [
            { name: '高速镜像节点', latencyMs: 42, ok: true },
            { name: '官方源节点', latencyMs: 320, ok: true }
          ],
          proxyCandidates: ['nvngx.dll', 'dxgi.dll', 'winmm.dll', 'version.dll'],
          gameDir: 'D:\\SteamLibrary\\steamapps\\common\\BlackMythWukong\\b1\\Binaries\\Win64'
        },
        dlssPreflight: {
          passed: true,
          blockingCount: 0,
          items: [
            { key: 'gameDir', label: '游戏目录', ok: true, blocking: true, detail: '已就绪' },
            { key: 'runtime', label: 'DLSS 运行库', ok: true, blocking: true, detail: 'v0.4.0 完整' },
            { key: 'writeable', label: '写入权限', ok: true, blocking: true, detail: '可写' }
          ]
        },
        overlayState: {
          nvAppInstalled: true,
          nvAppPath: 'C:\\Program Files\\NVIDIA Corporation\\NVIDIA app\\CEF\\NVIDIA app.exe',
          patched: false,
          backupExists: true,
          cefCacheSize: '182 MB',
          altRReady: false,
          altZReady: false
        },
        dualState: {
          hasRenderGpu: true,
          renderGpuName: 'NVIDIA CMP 90HX',
          renderGpuPciId: '10DE:220D',
          hasDisplayGpu: true,
          displayGpuName: 'AMD Radeon Graphics',
          displayGpuPciId: '1002:1638',
          hasVirtualGpu: false,
          virtualGpuName: '',
          boundGamesCount: 2,
          hagsActive: true,
          boundApps: [
            { exePath: 'D:\\SteamLibrary\\steamapps\\common\\BlackMythWukong\\b1\\Binaries\\Win64\\b1-Win64-Shipping.exe', name: 'b1-Win64-Shipping.exe', addedAt: '2026-10-19' },
            { exePath: 'D:\\SteamLibrary\\steamapps\\common\\Cyberpunk 2077\\bin\\x64\\Cyberpunk2077.exe', name: 'Cyberpunk2077.exe', addedAt: '2026-10-18' }
          ]
        },
        maintenanceSnapshots: {
          items: [
            { id: 'snap-20261020-114208', label: '伪装前自动备份', createdAt: '2026-10-20 11:42:08', sizeBytes: 245760, kind: 'auto', restorable: true },
            { id: 'snap-20261018-091532', label: '出厂基线备份', createdAt: '2026-10-18 09:15:32', sizeBytes: 512000, kind: 'factory', restorable: true }
          ]
        },
        logsInitial: [
          { at: '2026-10-20 17:20:01', level: 'audit', text: '已通过管理员权限启动，通信连接正常' },
          { at: '2026-10-20 17:20:02', level: 'info', text: '硬件检测完成：槽位 #0 NVIDIA CMP 90HX (GA102)' },
          { at: '2026-10-20 17:21:15', level: 'info', text: '引导状态检测完成：UEFI 直启已生效' },
          { at: '2026-10-20 17:22:31', level: 'warn', text: '空闲达 8 秒阈值，已切换待机节能状态（功耗 36.5W）' }
        ],
        updateState: {
          currentVersion: '6.1.0',
          latestVersion: '6.1.2',
          hasUpdate: true,
          releaseNotes: '· 优化界面排版与图标细节\n· 支持 90HX/50HX/40HX 四通道解锁\n· 扩展 6 大架构 76 款显卡伪装型号\n· 增强更新包签名与完整性校验',
          downloadUrl: '',
          assetName: 'HXStudio_v6.1.2.exe',
          sha256: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
          verified: false
        }
      };

      function bootMockDataPipe() {
        if (typeof MOCK !== 'object' || !MOCK) return;
        const pushEvt = (action, payload) => handleIncomingEnvelope({ kind: 'evt', id: null, action: action, payload: payload });
        pushEvt('theme.apply', { theme: MOCK.theme });
        pushEvt('device.snapshot', JSON.parse(JSON.stringify(MOCK.deviceSnapshot)));
        pushEvt('gpu.list', JSON.parse(JSON.stringify(MOCK.gpuList)));
        pushEvt('unlock.state', JSON.parse(JSON.stringify(MOCK.unlockState)));
        pushEvt('spoof.models', JSON.parse(JSON.stringify(MOCK.spoofModels)));
        pushEvt('spoof.state', JSON.parse(JSON.stringify(MOCK.spoofState)));
        pushEvt('power.state', JSON.parse(JSON.stringify(MOCK.powerState)));
        pushEvt('dlss.state', JSON.parse(JSON.stringify(MOCK.dlssState)));
        pushEvt('dlss.preflight', JSON.parse(JSON.stringify(MOCK.dlssPreflight)));
        pushEvt('overlay.state', JSON.parse(JSON.stringify(MOCK.overlayState)));
        pushEvt('dual.state', JSON.parse(JSON.stringify(MOCK.dualState)));
        pushEvt('maintenance.snapshots', JSON.parse(JSON.stringify(MOCK.maintenanceSnapshots)));
        pushEvt('update.state', JSON.parse(JSON.stringify(MOCK.updateState)));
        (MOCK.logsInitial || []).forEach(entry => pushEvt('logs.append', entry));
      }

      function dispatchToMockHost(envelope) {
        const { id, action, payload } = envelope;
        const replyOk = (data) => setTimeout(() => {
          handleIncomingEnvelope({ kind: 'res', id: id, action: action, payload: { ok: true, data: data || {}, error: null } });
        }, 180);
        const pushEvt = (act, pl) => handleIncomingEnvelope({ kind: 'evt', id: null, action: act, payload: pl });

        if (!window.MOCK && typeof MOCK === 'undefined') { replyOk({}); return; }
        if (!MOCK) {
          replyOk({});
          return;
        }

        switch (action) {
          case 'device.refresh':
            pushEvt('sensors.tick', {
              temp: 56 + Math.floor(Math.random() * 8),
              powerDraw: +(135 + Math.random() * 25).toFixed(1),
              utilPercent: Math.floor(Math.random() * 30)
            });
            replyOk({ message: '硬件与传感器数据已刷新' });
            break;
          case 'unlock.install':
            MOCK.unlockState.mode = payload.channel === 'D' ? 'None' : (payload.channel === 'B' ? 'Mode2_UefiShell' : 'Mode1_DirectBoot');
            MOCK.unlockState.modeLabel = '卡型 ' + payload.cardType + ' · 通道 ' + payload.channel + ' 已配置';
            MOCK.unlockState.channels.forEach(c => { c.installed = (c.id === payload.channel); });
            pushEvt('unlock.state', JSON.parse(JSON.stringify(MOCK.unlockState)));
            pushEvt('logs.append', { at: '2026-10-20 17:38:00', level: 'audit', text: '执行 unlock.install: 卡型=' + payload.cardType + ', 通道=' + payload.channel });
            replyOk({ message: '引导通道 [' + payload.channel + '] 操作已完成' });
            break;
          case 'unlock.rollback':
            MOCK.unlockState.mode = 'None';
            MOCK.unlockState.modeLabel = '官方默认引导 (未注入)';
            MOCK.unlockState.channels.forEach(c => { c.installed = false; });
            pushEvt('unlock.state', JSON.parse(JSON.stringify(MOCK.unlockState)));
            replyOk({ message: '已恢复官方默认引导' });
            break;
          case 'unlock.scan':
            pushEvt('unlock.state', JSON.parse(JSON.stringify(MOCK.unlockState)));
            replyOk({ message: '引导状态检测完成' });
            break;
          case 'spoof.apply':
            MOCK.spoofState.gpus[0].currentDesc = payload.targetName;
            MOCK.spoofState.gpus[0].targetName = payload.targetName;
            MOCK.spoofState.gpus[0].isSpoofed = true;
            MOCK.deviceSnapshot.gpu.displayName = payload.targetName;
            MOCK.deviceSnapshot.gpu.isSpoofed = true;
            pushEvt('spoof.state', JSON.parse(JSON.stringify(MOCK.spoofState)));
            pushEvt('device.snapshot', JSON.parse(JSON.stringify(MOCK.deviceSnapshot)));
            pushEvt('logs.append', { at: '2026-10-20 17:38:15', level: 'audit', text: '显卡伪装已应用: Slot #' + payload.gpuSlot + ' -> ' + payload.targetName });
            replyOk({ message: '已伪装为 ' + payload.targetName + ' 并完成驱动重载' });
            break;
          case 'spoof.restore':
            MOCK.spoofState.gpus[0].currentDesc = MOCK.deviceSnapshot.gpu.name;
            MOCK.spoofState.gpus[0].isSpoofed = false;
            MOCK.deviceSnapshot.gpu.displayName = MOCK.deviceSnapshot.gpu.name;
            MOCK.deviceSnapshot.gpu.isSpoofed = false;
            pushEvt('spoof.state', JSON.parse(JSON.stringify(MOCK.spoofState)));
            pushEvt('device.snapshot', JSON.parse(JSON.stringify(MOCK.deviceSnapshot)));
            replyOk({ message: '已还原出厂显卡名称' });
            break;
          case 'power.enable':
            MOCK.powerState.enabled = true;
            MOCK.powerState.running = true;
            MOCK.powerState.idleSeconds = payload.idleSeconds || 8;
            pushEvt('power.state', JSON.parse(JSON.stringify(MOCK.powerState)));
            replyOk({ message: '省电守护进程已启动 (空闲阈值 ' + MOCK.powerState.idleSeconds + 's)' });
            break;
          case 'power.disable':
            MOCK.powerState.enabled = false;
            MOCK.powerState.running = false;
            pushEvt('power.state', JSON.parse(JSON.stringify(MOCK.powerState)));
            replyOk({ message: '省电守护进程已停止' });
            break;
          case 'power.setPreset':
            MOCK.powerState.preset = payload.preset;
            MOCK.powerState.currentPstate = payload.preset === 'turbo' ? 'P0' : 'P8';
            MOCK.powerState.powerRange.current = payload.preset === 'turbo' ? 320 : (payload.preset === 'sleep' ? 120 : 250);
            pushEvt('power.state', JSON.parse(JSON.stringify(MOCK.powerState)));
            replyOk({ message: '已切换功耗预设至 ' + payload.preset });
            break;
          case 'dlss.preflight': {
            const hasDir = !!(payload.gameDir && payload.gameDir.trim());
            MOCK.dlssPreflight = {
              passed: hasDir,
              blockingCount: hasDir ? 0 : 1,
              items: [
                { key: 'gameDir', label: '游戏目录是否存在', ok: hasDir, blocking: true, detail: hasDir ? ('已验证: ' + payload.gameDir) : '游戏目录为空或不存在' },
                { key: 'runtime', label: 'DLSS 运行库是否就绪', ok: true, blocking: true, detail: 'v' + MOCK.dlssState.localVersion + ' 已就绪' },
                { key: 'writeable', label: '目录写入权限检查', ok: hasDir, blocking: true, detail: hasDir ? '可写' : '目录无效' }
              ]
            };
            pushEvt('dlss.preflight', JSON.parse(JSON.stringify(MOCK.dlssPreflight)));
            replyOk({ message: hasDir ? '前置检查全部通过' : '前置检查发现阻塞项' });
            break;
          }
          case 'dlss.deploy':
          case 'dlss.checkUpdate': {
            let pct = 0;
            const timer = setInterval(() => {
              pct += 50;
              handleIncomingEnvelope({
                kind: 'prg',
                id: id,
                action: action,
                payload: { percent: Math.min(100, pct), stage: action === 'dlss.deploy' ? '正在写入 DLL 与配置' : '正在同步运行库镜像', detail: pct + '%' }
              });
              if (pct >= 100) {
                clearInterval(timer);
                if (action === 'dlss.checkUpdate') {
                  MOCK.dlssState.localVersion = MOCK.dlssState.latestVersion;
                  MOCK.dlssState.hasUpdate = false;
                  pushEvt('dlss.state', JSON.parse(JSON.stringify(MOCK.dlssState)));
                }
                replyOk({ message: action === 'dlss.deploy' ? 'DLSS 补丁已成功部署到游戏目录' : 'DLSS 运行库已更新至最新版' });
              }
            }, 140);
            break;
          }
          case 'dlss.remove':
            replyOk({ message: '已移除目标游戏目录中的 DLSS 补丁' });
            break;
          case 'overlay.unlock':
            MOCK.overlayState.patched = true;
            MOCK.overlayState.altRReady = true;
            MOCK.overlayState.altZReady = true;
            pushEvt('overlay.state', JSON.parse(JSON.stringify(MOCK.overlayState)));
            replyOk({ message: '已解除 NVIDIA App 游戏覆盖白名单限制' });
            break;
          case 'overlay.restore':
            MOCK.overlayState.patched = false;
            MOCK.overlayState.altRReady = false;
            MOCK.overlayState.altZReady = false;
            pushEvt('overlay.state', JSON.parse(JSON.stringify(MOCK.overlayState)));
            replyOk({ message: '已还原 NVIDIA App 原始文件' });
            break;
          case 'overlay.cleanCache':
            MOCK.overlayState.cefCacheSize = '0 MB (已清理)';
            pushEvt('overlay.state', JSON.parse(JSON.stringify(MOCK.overlayState)));
            replyOk({ message: 'CEF 缓存清理完成' });
            break;
          case 'dual.bind': {
            const parts = String(payload.exePath).split(/[/\\]/);
            const name = parts[parts.length - 1] || 'game.exe';
            MOCK.dualState.boundApps.unshift({ exePath: payload.exePath, name: name, addedAt: '2026-10-20' });
            MOCK.dualState.boundGamesCount = MOCK.dualState.boundApps.length;
            pushEvt('dual.state', JSON.parse(JSON.stringify(MOCK.dualState)));
            replyOk({ message: '已绑定 ' + name + ' 到高性能渲染卡' });
            break;
          }
          case 'dual.unbind':
            MOCK.dualState.boundApps = MOCK.dualState.boundApps.filter(a => a.exePath !== payload.exePath);
            MOCK.dualState.boundGamesCount = MOCK.dualState.boundApps.length;
            pushEvt('dual.state', JSON.parse(JSON.stringify(MOCK.dualState)));
            replyOk({ message: '已解除程序绑定' });
            break;
          case 'dual.scanGames':
            replyOk({ message: '已完成常见游戏目录扫描' });
            break;
          case 'snapshot.create': {
            const newId = 'snap-20261020-' + Math.floor(100000 + Math.random() * 899999);
            MOCK.maintenanceSnapshots.items.unshift({
              id: newId,
              label: payload.label || '手动创建快照',
              createdAt: '2026-10-20 17:39:00',
              sizeBytes: 262144,
              kind: 'manual',
              restorable: true
            });
            pushEvt('maintenance.snapshots', JSON.parse(JSON.stringify(MOCK.maintenanceSnapshots)));
            replyOk({ message: '已创建还原点 ' + newId });
            break;
          }
          case 'snapshot.restore':
            replyOk({ message: '已无损回滚至快照 ' + payload.snapshotId });
            break;
          case 'snapshot.delete':
            MOCK.maintenanceSnapshots.items = MOCK.maintenanceSnapshots.items.filter(i => i.id !== payload.snapshotId);
            pushEvt('maintenance.snapshots', JSON.parse(JSON.stringify(MOCK.maintenanceSnapshots)));
            replyOk({ message: '快照已删除' });
            break;
          case 'factory.reset':
            MOCK.spoofState.gpus[0].isSpoofed = false;
            MOCK.spoofState.gpus[0].currentDesc = MOCK.deviceSnapshot.gpu.name;
            MOCK.unlockState.mode = 'None';
            MOCK.unlockState.modeLabel = '未注入 (已恢复出厂)';
            MOCK.unlockState.channels.forEach(c => { c.installed = false; });
            pushEvt('spoof.state', JSON.parse(JSON.stringify(MOCK.spoofState)));
            pushEvt('unlock.state', JSON.parse(JSON.stringify(MOCK.unlockState)));
            replyOk({ message: '紧急出厂重置已完成，所有设置已恢复初始状态' });
            break;
          case 'logs.query':
            replyOk({
              items: [
                { at: '2026-10-20 17:15:00', level: 'info', text: '历史分页日志记录 #1 (offset=' + payload.offset + ')' },
                { at: '2026-10-20 17:16:12', level: 'audit', text: '历史分页日志记录 #2: CfgMgr32 设备树枚举通过' }
              ]
            });
            break;
          case 'update.check':
            pushEvt('update.state', JSON.parse(JSON.stringify(MOCK.updateState)));
            replyOk({ message: '检查更新完成：最新版本 ' + MOCK.updateState.latestVersion });
            break;
          case 'update.download': {
            let upPct = 0;
            const uTimer = setInterval(() => {
              upPct += 50;
              handleIncomingEnvelope({
                kind: 'prg',
                id: id,
                action: action,
                payload: { percent: Math.min(100, upPct), stage: '正在下载更新包并计算 SHA-256', detail: upPct + '%' }
              });
              if (upPct >= 100) {
                clearInterval(uTimer);
                MOCK.updateState.verified = true;
                pushEvt('update.state', JSON.parse(JSON.stringify(MOCK.updateState)));
                replyOk({ message: '更新包下载完成且 SHA-256 校验通过，现可应用更新' });
              }
            }, 150);
            break;
          }
          case 'update.apply':
            replyOk({ message: '正在启动更新替换脚本并重启程序...' });
            break;
          case 'dialog.browse':
            replyOk({
              path: payload.kind === 'file'
                ? 'D:\\SteamLibrary\\steamapps\\common\\ForzaHorizon5\\ForzaHorizon5.exe'
                : 'D:\\SteamLibrary\\steamapps\\common\\BlackMythWukong\\b1\\Binaries\\Win64'
            });
            break;
          default:
            replyOk({});
            break;
        }
      }

      /* =======================================================================
         自绘提示框
         原生 title 属性会弹出系统那个灰黄方框，和界面完全不搭。
         这里把 title 统一转成 data-tip，用自绘浮层显示；新插入的节点在首次
         悬停时也会被自动转换，所以模板里不用改任何标记。
         ======================================================================= */
      (function initHxTooltip() {
        let tipEl = null;

        // 只保留右上角窗口三键的悬停提示，其余页面一律不弹
        const TIP_ALLOWED = '.window-dots';

        function allowed(el) {
          return !!(el && el.closest && el.closest(TIP_ALLOWED));
        }

        function convert(el) {
          if (!el || !el.getAttribute) return null;
          const t = el.getAttribute('title');
          if (allowed(el)) {
            if (t) {
              el.setAttribute('data-tip', t);
              el.removeAttribute('title');
            }
            return el.getAttribute('data-tip');
          }
          // 非白名单：把 title 和 data-tip 一起抹掉，原生框和自绘框都不会出现
          if (t) el.removeAttribute('title');
          if (el.hasAttribute('data-tip')) el.removeAttribute('data-tip');
          return null;
        }

        document.querySelectorAll('[title]').forEach(convert);

        // 渲染函数会通过 innerHTML 插入带 title 的节点，这里持续兜住，
        // 保证原生系统小方框一次都不会弹出来
        try {
          const sweep = function () {
            const list = document.querySelectorAll('[title]');
            for (let i = 0; i < list.length; i++) convert(list[i]);
          };
          new MutationObserver(sweep).observe(document.body, { childList: true, subtree: true });
          setTimeout(sweep, 800);
          setTimeout(sweep, 2500);
        } catch (e) { }

        function ensureTip() {
          if (tipEl) return tipEl;
          tipEl = document.createElement('div');
          tipEl.className = 'hx-tip';
          tipEl.setAttribute('role', 'tooltip');
          document.body.appendChild(tipEl);
          return tipEl;
        }

        function place(x, y) {
          const r = tipEl.getBoundingClientRect();
          let left = x + 14;
          let top = y + 18;
          if (left + r.width > window.innerWidth - 8) left = window.innerWidth - r.width - 8;
          if (top + r.height > window.innerHeight - 8) top = y - r.height - 14;
          tipEl.style.left = Math.max(8, left) + 'px';
          tipEl.style.top = Math.max(8, top) + 'px';
        }

        function hideTip() {
          if (tipEl) tipEl.classList.remove('show');
        }

        document.addEventListener('mouseover', function (e) {
          const el = e.target && e.target.closest ? e.target.closest('[data-tip],[title]') : null;
          if (!el) { hideTip(); return; }
          const text = convert(el);
          if (!text) { hideTip(); return; }
          const box = ensureTip();
          if (box.textContent !== text) box.textContent = text;
          box.classList.add('show');
          place(e.clientX, e.clientY);
        });

        document.addEventListener('mousemove', function (e) {
          if (tipEl && tipEl.classList.contains('show')) place(e.clientX, e.clientY);
        });

        document.addEventListener('mouseout', function (e) {
          const el = e.target && e.target.closest ? e.target.closest('[data-tip]') : null;
          if (el) hideTip();
        });

        window.addEventListener('blur', hideTip);
        document.addEventListener('click', hideTip);
      })();

      /* 型号清单是静态能力表（6 大架构 76 款），不属于运行时数据。
         必须放在 MOCK 定义之后执行——首次渲染发生在 MOCK 赋值之前，
         那时引用它还是 undefined，下拉框就会一直停在「等待宿主注入」。 */
      /* 型号目录由宿主通过 spoof.models 事件下发，前端只负责渲染。
         这里的 MOCK 兜底**仅用于浏览器直接打开预览**（无宿主时），
         程序内不会再走这条路，避免出现「前端一份、后端一份」的双份维护。 */
      try {
        if (!hasHost && !state.spoofModels && typeof MOCK !== 'undefined' && MOCK && MOCK.spoofModels) {
          state.spoofModels = MOCK.spoofModels;
          if (!state.selectedSpoofTarget && state.spoofModels.recommended) {
            state.selectedSpoofTarget = state.spoofModels.recommended;
          }
          renderSpoofModelsDropdown();
          renderSpoofPage();
        }
        // 诊断：把页面侧看到的真实情况回传宿主，写进日志
        var diagSel = document.getElementById('spoofModelSelect');
        sendCmd('app.diag', {
          tag: 'spoofModels',
          groups: state.spoofModels && state.spoofModels.groups ? state.spoofModels.groups.length : -1,
          firstGroupModels: (state.spoofModels && state.spoofModels.groups && state.spoofModels.groups[0] && state.spoofModels.groups[0].models)
            ? state.spoofModels.groups[0].models.length : -1,
          optgroups: diagSel ? diagSel.querySelectorAll('optgroup').length : -1,
          options: diagSel && diagSel.options ? diagSel.options.length : -1,
          htmlLen: diagSel ? diagSel.innerHTML.length : -1,
          htmlHead: diagSel ? diagSel.innerHTML.slice(0, 60) : '',
          selectTag: diagSel ? diagSel.tagName : 'none',
          hasWait: diagSel ? diagSel.innerHTML.indexOf('等待宿主') >= 0 : null,
          host: hasHost,
          mock: usingMock
        });
      } catch (e) { }
    })();
  
