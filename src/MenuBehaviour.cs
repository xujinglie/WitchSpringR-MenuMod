using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WSMenuMod
{
    /// <summary>
    /// 菜单本体：IMGUI 绘制，F8 开关，F10 截图。
    /// 设计分辨率 1920x1080，运行时按 Screen.width 自动缩放。
    /// </summary>
    internal class MenuBehaviour : MonoBehaviour
    {
        private const float DesignW = 1920f;
        private const float DesignH = 1080f;

        private bool _open;
        private int _tab;
        private Rect _win = new Rect(60f, 40f, 1000f, 790f);
        private bool _dragging;
        private Vector2 _dragOffset;
        private float _uiScale = 1f;

        private bool _stylesReady;
        private bool _cjkOk;
        private Font _font;
        private GUIStyle _label, _labelDim, _title, _button, _buttonSmall, _field, _header, _warn, _tabOn;

        private float _lastToggleTime = -10f;
        private float _lastCaptureTime = -10f;
        private bool _f8Held, _f10Held, _f9Held;
        private bool _pollAvailable;
        private bool _inputWarned;

        // 角色属性编辑缓冲
        private readonly Dictionary<string, string> _statBuf = new Dictionary<string, string>();
        private string _goldBuf = "";
        private bool _statBufDirty = true;

        // 背包
        private string _search = "";
        private string _addCount = "1";
        private string _lastSearch = null;
        private readonly List<KeyValuePair<string, string>> _filtered = new List<KeyValuePair<string, string>>();
        private Vector2 _scrollInv, _scrollCat, _scrollRegen, _scrollEnemy;

        // 刷新
        private string _multBuf = "1";
        private string _remainBuf = "0";
        private string _regenInfo = "";
        private string _itemInfo = "";

        // 数据快照
        private float _snapTime = -99f;
        private bool _inGame;
        private List<Game.ItemRow> _inv = new List<Game.ItemRow>();
        private List<Game.RegenRow> _regen = new List<Game.RegenRow>();
        private List<Game.EnemyRow> _enemies = new List<Game.EnemyRow>();
        private List<Game.EnemyStatRow> _esRows = new List<Game.EnemyStatRow>();

        // 敌人属性
        private string _esSearch = "";
        private string _lastEsSearch = null;
        private readonly List<Game.EnemyStatRow> _esFiltered = new List<Game.EnemyStatRow>();
        private List<Game.EnemyStatRow> _esFilteredRef;
        private Vector2 _scrollEs;
        private string _esSel = "";
        private string _esHp = "", _esStr = "", _esSp = "", _esDef = "", _esAgt = "";
        private string _esBodySize = "", _esBodyType = "", _esSkillType = "";
        private string _esDefPhys = "", _esDefMagic = "";
        private string _esMultBuf = "1";
        private string _esInfo = "";
        private int _esLive;
        private float _lastLiveCheck = -99f;
        private float _lastEnforce = -99f;

        // 光标
        private bool _cursorSaved;
        private bool _prevCursorVisible;
        private CursorLockMode _prevLock;

        private string T(string zh, string en) => _cjkOk ? zh : en;

        private static string Cut(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        // ------------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------------

        private void Update()
        {
            // 游戏使用新版 Input System（旧版 Input 多半被禁用），优先轮询 Input System。
            // 用"按住锁存"的方式做边沿检测，避免和 OnGUI 事件重复触发。
            bool d8 = false, d10 = false, polled = false, d9 = false;
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    polled = true;
                    _pollAvailable = true;
                    d8 = kb.f8Key.isPressed;
                    d10 = kb.f10Key.isPressed;
                    d9 = kb.f9Key.isPressed;
                }
            }
            catch (Exception e)
            {
                if (!_inputWarned)
                {
                    _inputWarned = true;
                    Plugin.LogSource?.LogWarning("[输入] Input System 轮询失败: " + e.GetType().Name + " " + e.Message);
                }
            }

            if (!polled)
            {
                try
                {
                    d8 = Input.GetKey(KeyCode.F8);
                    d10 = Input.GetKey(KeyCode.F10);
                    _pollAvailable = true;
                }
                catch { }
            }

            if (d8 && !_f8Held) { _f8Held = true; Toggle(); }
            else if (!d8) _f8Held = false;

            if (d10 && !_f10Held) { _f10Held = true; Capture(); }
            else if (!d10) _f10Held = false;

            if (d9 && !_f9Held) { _f9Held = true; SafeDiagnostics(); }
            else if (!d9) _f9Held = false;

            // 定期刷新"是否在游戏内"状态（同时用于诊断日志）
            if (Time.unscaledTime - _lastStateCheck > 1f)
            {
                _lastStateCheck = Time.unscaledTime;
                try
                {
                    _inGame = Game.InGame;
                    LogStateOnce();
                }
                catch { }
            }


            // 敌人数值覆盖：每隔 1 秒检查一次，把新出现的实例套上改过的数值
            if (Time.unscaledTime - _lastEnforce > 1f)
            {
                _lastEnforce = Time.unscaledTime;
                try { if (Game.EnemyOverrideCount > 0) Game.EnforceEnemyOverrides(); } catch { }
            }
        }

        private void HandleHotkeys()
        {
            var e = Event.current;
            if (e == null) return;

            // 注意：IMGUI 对未消费的事件会逐帧重复投递，所以必须先 Use() 再用锁存去重。
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.F8)
                {
                    e.Use();
                    if (_pollAvailable) return;
                    if (!_f8Held) { _f8Held = true; Toggle(); }
                    return;
                }
                if (e.keyCode == KeyCode.F10)
                {
                    e.Use();
                    if (!_f10Held) { _f10Held = true; Capture(); }
                    return;
                }
                if (e.keyCode == KeyCode.F9)
                {
                    e.Use();
                    if (_pollAvailable) return;
                    if (!_f9Held) { _f9Held = true; SafeDiagnostics(); }
                    return;
                }
            }
            else if (e.type == EventType.KeyUp)
            {
                if (e.keyCode == KeyCode.F8) { e.Use(); if (!_pollAvailable) _f8Held = false; return; }
                if (e.keyCode == KeyCode.F10) { e.Use(); _f10Held = false; return; }
            }
        }

        private void OnDisable()
        {
            RestoreCursor();
            ReleaseModal();
        }

        private void Toggle()
        {
            // 去抖：IMGUI 一帧内可能收到多次 KeyDown 事件
            if (Time.unscaledTime - _lastToggleTime < 0.25f) return;
            _lastToggleTime = Time.unscaledTime;

            _open = !_open;
            Plugin.LogSource?.LogInfo("[菜单] " + (_open ? "打开" : "关闭"));
            if (_open)
            {
                _statBufDirty = true;
                _snapTime = -99f;
                SaveAndFreeCursor();
                ApplyModal();
                RefreshSnapshots(true);
            }
            else
            {
                RestoreCursor();
                ReleaseModal();
            }
        }

        // 菜单打开时阻断游戏自身的 UI / 角色操作，避免点菜单时误触游戏
        private bool _modal;
        private UnityEngine.EventSystems.EventSystem _es;

        private void ApplyModal()
        {
            if (_modal) return;
            _modal = true;
            try
            {
                var so = Game.SO;
                if (so != null)
                {
                    var pc = so.pc;
                    if (pc != null && pc.Pointer != IntPtr.Zero) pc.AddControlPause("WSMENUMOD");
                }
            }
            catch { }
            try
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.Pointer != IntPtr.Zero)
                {
                    _es = es;
                    es.enabled = false;
                }
            }
            catch { }
        }

        private void ReleaseModal()
        {
            if (!_modal) return;
            _modal = false;
            try
            {
                var so = Game.SO;
                if (so != null)
                {
                    var pc = so.pc;
                    if (pc != null && pc.Pointer != IntPtr.Zero) pc.RemoveControlPauseID("WSMENUMOD");
                }
            }
            catch { }
            try
            {
                if (_es != null && _es.Pointer != IntPtr.Zero) _es.enabled = true;
            }
            catch { }
            _es = null;
        }

        private void SaveAndFreeCursor()
        {
            if (_cursorSaved) return;
            try
            {
                _prevCursorVisible = Cursor.visible;
                _prevLock = Cursor.lockState;
                _cursorSaved = true;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            catch { }
        }

        private void RestoreCursor()
        {
            if (!_cursorSaved) return;
            try
            {
                Cursor.visible = _prevCursorVisible;
                Cursor.lockState = _prevLock;
            }
            catch { }
            _cursorSaved = false;
        }

        private void SafeDiagnostics()
        {
            try { Game.DumpDiagnostics(); }
            catch (Exception e) { Plugin.LogSource?.LogWarning("[诊断] 失败: " + e.Message); }
        }

        private void Capture()
        {
            if (Time.unscaledTime - _lastCaptureTime < 0.5f) return;
            _lastCaptureTime = Time.unscaledTime;
            try
            {
                string path = System.IO.Path.GetFullPath(Application.dataPath + "/../menu_capture.png");
                ScreenCapture.CaptureScreenshot(path);
                Plugin.LogSource?.LogInfo("[截图] " + path);
            }
            catch (Exception e)
            {
                Plugin.LogSource?.LogWarning("[截图] 失败: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // 数据快照
        // ------------------------------------------------------------------

        private void RefreshSnapshots(bool force)
        {
            if (!force && Time.unscaledTime - _snapTime < 0.75f) return;
            _snapTime = Time.unscaledTime;
            try
            {
                _inGame = Game.InGame;
                _inv = Game.SnapshotInventory();
                _regen = Game.SnapshotRegen();
                _enemies = Game.SnapshotEnemies();
                _esRows = Game.SnapshotEnemyStats();
                LogStateOnce();
            }
            catch { }
        }

        private string _lastState = "";
        private float _lastStateCheck = -10f;
        private void LogStateOnce()
        {
            string scene = "?";
            try { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; } catch { }
            string st = scene + "|" + _inGame;
            if (st == _lastState) return;
            _lastState = st;
            int hp = 0, maxHp = 0;
            try { var p = Game.PS; if (p != null) { hp = p.hp; maxHp = p.maxHp; } } catch { }
            Plugin.LogSource?.LogInfo("[状态] 场景=" + scene + " 在游戏内=" + _inGame + " hp=" + hp + "/" + maxHp
                + " 背包=" + _inv.Count + " 待刷新=" + _regen.Count + " 怪物表=" + _enemies.Count);
        }

        // ------------------------------------------------------------------
        // OnGUI
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            try
            {
                EnsureStyles();
                HandleHotkeys();

                _uiScale = Mathf.Max(0.5f, Screen.width / DesignW);
                GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1f));

                if (!_open)
                {
                    DrawHint();
                    return;
                }
                DrawMenu();
            }
            catch (Exception e)
            {
                Plugin.LogSource?.LogError("[菜单] OnGUI 异常: " + e);
            }
        }

        private Vector2 MousePos()
        {
            var e = Event.current;
            if (e == null) return Vector2.zero;
            var m = e.mousePosition;
            return new Vector2(m.x / _uiScale, m.y / _uiScale);
        }

        private void DrawHint()
        {
            if (!Game.Ready) return;
            GUI.Label(new Rect(14f, 14f, 260f, 26f), T("F8 打开菜单", "F8 Menu"), _labelDim);
        }

        private void DrawMenu()
        {
            // 拖动
            var e = Event.current;
            var mouse = MousePos();
            var titleBar = new Rect(_win.x, _win.y, _win.width, 40f);
            if (e != null)
            {
                if (e.type == EventType.MouseDown && titleBar.Contains(mouse))
                {
                    _dragging = true;
                    _dragOffset = mouse - new Vector2(_win.x, _win.y);
                }
                else if (e.type == EventType.MouseUp) _dragging = false;
                else if (e.type == EventType.MouseDrag && _dragging)
                {
                    var pos = mouse - _dragOffset;
                    _win.x = Mathf.Clamp(pos.x, -_win.width + 160f, DesignW - 160f);
                    _win.y = Mathf.Clamp(pos.y, 0f, DesignH - 60f);
                }
            }

            GUI.Box(_win, GUIContent.none);
            GUI.Box(new Rect(_win.x, _win.y, _win.width, 40f), GUIContent.none);
            GUI.Label(new Rect(_win.x + 16f, _win.y + 8f, 500f, 26f), T("WitchSpring R 修改菜单", "WitchSpring R Menu"), _title);
            GUI.Label(new Rect(_win.x + _win.width - 300f, _win.y + 10f, 220f, 22f),
                T("F8 关闭 / F10 截图", "F8 close / F10 shot"), _labelDim);
            if (GUI.Button(new Rect(_win.x + _win.width - 40f, _win.y + 8f, 28f, 26f), "X"))
                Toggle();

            // 标签页
            string[] zh = { "角色属性", "背包物品", "怪物刷新", "敌人属性" };
            string[] en = { "Character", "Inventory", "Monster Respawn", "Enemy Stats" };
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(_win.x + 16f + i * 160f, _win.y + 50f, 150f, 34f);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = _tab == i ? new Color(0.35f, 0.62f, 0.95f) : Color.white;
                if (GUI.Button(r, T(zh[i], en[i]), _button))
                {
                    if (_tab != i)
                    {
                        _tab = i;
                        _statBufDirty = true;
                        _snapTime = -99f;
                        RefreshSnapshots(true);
                    }
                }
                GUI.backgroundColor = prev;
            }

            var content = new Rect(_win.x + 16f, _win.y + 94f, _win.width - 32f, _win.height - 110f);
            GUI.Box(content, GUIContent.none);

            if (!Game.Ready)
            {
                GUI.Label(new Rect(content.x + 24f, content.y + 30f, content.width - 48f, 30f),
                    T("尚未进入游戏（标题画面 / 读档中不可用）。请先载入一个存档。",
                      "Not in game yet. Load a save first."), _warn);
                return;
            }
            if (!_inGame)
            {
                GUI.Label(new Rect(content.x + 24f, content.y + 30f, content.width - 48f, 30f),
                    T("检测不到玩家角色：请在游戏内（非标题/读档界面）使用。",
                      "Player not found: use this while actually in game."), _warn);
            }

            switch (_tab)
            {
                case 0: DrawCharacter(content); break;
                case 1: DrawInventory(content); break;
                case 2: DrawMonster(content); break;
                case 3: DrawEnemyStats(content); break;
            }
        }

        // ------------------------------------------------------------------
        // Tab 1: 角色属性
        // ------------------------------------------------------------------

        private void DrawCharacter(Rect area)
        {
            var ps = Game.PS;
            if (ps == null) return;
            if (_statBufDirty) SyncStatBuffers();

            float x = area.x + 20f, y = area.y + 14f;
            float labelW = 120f, fieldW = 110f, rowH = 31f;

            GUI.Label(new Rect(x, y, 420f, 24f), T("基础属性（左侧输入框 = 可编辑）", "Base stats (editable)"), _header);
            if (GUI.Button(new Rect(x + 300f, y - 3f, 110f, 26f), T("读取当前值", "Reload"), _buttonSmall))
                SyncStatBuffers();
            if (GUI.Button(new Rect(x + 418f, y - 3f, 110f, 26f), T("应用全部", "Apply all"), _buttonSmall))
                ApplyStatBuffers();
            y += 34f;

            for (int i = 0; i < Game.StatRows.Length; i++)
            {
                var row = Game.StatRows[i];
                float ry = y + i * rowH;
                GUI.Label(new Rect(x, ry + 4f, labelW, 24f), T(row.Zh, row.En), _label);

                string cur;
                if (!_statBuf.TryGetValue(row.Key, out cur)) cur = "";
                string next = GUI.TextField(new Rect(x + labelW, ry, fieldW, 26f), cur, _field);
                if (next != cur) _statBuf[row.Key] = next;

                int live = 0;
                try { live = row.Get(ps); } catch { }
                GUI.Label(new Rect(x + labelW + fieldW + 12f, ry + 4f, 150f, 24f),
                    T("当前 ", "now ") + live, _labelDim);
            }

            float gy = y + Game.StatRows.Length * rowH + 6f;
            GUI.Label(new Rect(x, gy + 4f, labelW, 24f), T("金币", "Gold"), _label);
            string gnext = GUI.TextField(new Rect(x + labelW, gy, fieldW, 26f), _goldBuf, _field);
            if (gnext != _goldBuf) _goldBuf = gnext;
            if (GUI.Button(new Rect(x + labelW + fieldW + 12f, gy, 80f, 26f), T("设定", "Set"), _buttonSmall))
            {
                if (int.TryParse(_goldBuf, out int gv)) Game.SetGold(gv);
                _statBufDirty = true;
            }

            // 右列
            float rx = area.x + 560f;
            float ry0 = area.y + 14f;

            GUI.Label(new Rect(rx, ry0, 380f, 24f), T("实际生效值（含装备加成）", "Effective values (with gear)"), _header);
            ry0 += 34f;
            DrawKV(rx, ry0, T("生命上限", "Max HP"), Game.GetComputed("maxHp")); ry0 += 27f;
            DrawKV(rx, ry0, T("魔力上限", "Max MP"), Game.GetComputed("maxMp")); ry0 += 27f;
            DrawKV(rx, ry0, T("力量", "STR"), Game.GetComputed("str")); ry0 += 27f;
            DrawKV(rx, ry0, T("防御", "DEF"), Game.GetComputed("def")); ry0 += 27f;
            DrawKV(rx, ry0, T("敏捷", "AGT"), Game.GetComputed("agt")); ry0 += 27f;
            DrawKV(rx, ry0, T("技能点", "SP"), Game.GetComputed("sp")); ry0 += 27f;
            DrawKV(rx, ry0, T("战斗等级", "Battle Lv"), Game.GetComputed("level")); ry0 += 40f;

            GUI.Label(new Rect(rx, ry0, 300f, 24f), T("快捷操作", "Quick actions"), _header);
            ry0 += 32f;

            float bw = 180f, bh = 32f, gap = 12f;
            if (GUI.Button(new Rect(rx, ry0, bw, bh), T("生命魔力全满", "Full HP/MP"), _button)) Game.HealFull();
            if (GUI.Button(new Rect(rx + bw + gap, ry0, bw, bh), T("力量/防御/敏捷 +50", "STR/DEF/AGT +50"), _button)) Game.BumpMainStats(50);
            ry0 += bh + gap;
            if (GUI.Button(new Rect(rx, ry0, bw, bh), T("技能点 +100", "SP +100"), _button)) { if (ps != null) ps.sp += 100; }
            if (GUI.Button(new Rect(rx + bw + gap, ry0, bw, bh), T("生命上限 +1000", "Max HP +1000"), _button)) { if (ps != null) { ps.maxHp += 1000; ps.hp += 1000; } }
            ry0 += bh + gap;
            if (GUI.Button(new Rect(rx, ry0, bw, bh), T("魔力上限 +500", "Max MP +500"), _button)) { if (ps != null) { ps.maxMp += 500; ps.mp += 500; } }
            if (GUI.Button(new Rect(rx + bw + gap, ry0, bw, bh), T("金币 +10000", "Gold +10000"), _button)) { Game.SetGold(Game.GetGold() + 10000); _statBufDirty = true; }
            ry0 += bh + 22f;
            if (GUI.Button(new Rect(rx, ry0, bw * 2 + gap, bh), T("全部基础属性设为 99", "Set all base stats to 99"), _button))
            {
                foreach (var row in Game.StatRows)
                {
                    if (row.Key == "maxHp" || row.Key == "maxMp" || row.Key == "hp" || row.Key == "mp") continue;
                    try { row.Set(ps, Math.Max(99, row.Get(ps))); } catch { }
                }
                _statBufDirty = true;
            }

            ry0 += bh + 20f;
            GUI.Label(new Rect(rx, ry0, 400f, 120f),
                T("提示：\n· 修改后的数值会写入当前存档内存，打开属性面板 / 存档时刷新显示。\n· “生命上限”受装备加成影响，实际值见右上“实际生效值”。",
                  "Tips:\n- Values live in memory and refresh when you open the status screen.\n- Max HP shown at right includes gear bonuses."), _labelDim);
        }

        private void DrawKV(float x, float y, string k, int v)
        {
            GUI.Label(new Rect(x, y, 220f, 24f), k, _label);
            GUI.Label(new Rect(x + 230f, y, 120f, 24f), v.ToString(), _label);
        }

        private void SyncStatBuffers()
        {
            _statBufDirty = false;
            var ps = Game.PS;
            if (ps == null) return;
            _statBuf.Clear();
            foreach (var row in Game.StatRows)
            {
                int v = 0;
                try { v = row.Get(ps); } catch { }
                _statBuf[row.Key] = v.ToString();
            }
            _goldBuf = Game.GetGold().ToString();
        }

        private void ApplyStatBuffers()
        {
            var ps = Game.PS;
            if (ps == null) return;
            foreach (var row in Game.StatRows)
            {
                if (!_statBuf.TryGetValue(row.Key, out string s)) continue;
                if (!int.TryParse(s, out int v)) continue;
                if (v < 0) v = 0;
                try { row.Set(ps, v); } catch { }
            }
            if (int.TryParse(_goldBuf, out int g)) Game.SetGold(g);
            Game.RefreshHud();
            SyncStatBuffers();
            Plugin.LogSource?.LogInfo("[属性] 已应用修改");
        }

        // ------------------------------------------------------------------
        // Tab 2: 背包
        // ------------------------------------------------------------------

        private void DrawInventory(Rect area)
        {
            RefreshSnapshots(false);
            Game.EnsureCatalog();
            RebuildFilter();

            float x = area.x + 20f;
            float y = area.y + 14f;

            GUI.Label(new Rect(x, y, 90f, 24f), T("搜索", "Search"), _label);
            string ns = GUI.TextField(new Rect(x + 62f, y - 2f, 250f, 28f), _search, _field);
            if (ns != _search) { _search = ns; _lastSearch = null; }

            GUI.Label(new Rect(x + 328f, y, 60f, 24f), T("数量", "Count"), _label);
            _addCount = GUI.TextField(new Rect(x + 374f, y - 2f, 70f, 28f), _addCount, _field);

            if (GUI.Button(new Rect(x + 458f, y - 2f, 140f, 28f), T("重新载入物品库", "Reload catalog"), _buttonSmall))
                Game.ClearCatalog();

            if (!string.IsNullOrEmpty(_itemInfo))
                GUI.Label(new Rect(x + 614f, y, 330f, 24f), _itemInfo, _labelDim);

            float listTop = area.y + 78f;
            float listH = area.height - 94f;
            float colW = area.width * 0.46f;

            GUI.Label(new Rect(x, listTop - 24f, colW, 22f),
                T("背包内物品 (" + _inv.Count + ")", "Inventory (" + _inv.Count + ")"), _header);

            var leftRect = new Rect(x, listTop, colW, listH);
            GUI.Box(leftRect, GUIContent.none);
            float rowH = 28f;
            var view = new Rect(0f, 0f, colW - 24f, Math.Max(listH, _inv.Count * rowH + 6f));
            _scrollInv = GUI.BeginScrollView(leftRect, _scrollInv, view);

            int start = Math.Max(0, (int)(_scrollInv.y / rowH) - 1);
            int end = Math.Min(_inv.Count, start + (int)(listH / rowH) + 3);
            for (int i = start; i < end; i++)
            {
                var it = _inv[i];
                float ry = i * rowH;
                string nm = Game.ItemName(it.Id);
                GUI.Label(new Rect(8f, ry + 3f, colW - 230f, 24f), Cut(nm, 22) + "  x" + it.Count, _label);

                if (GUI.Button(new Rect(colW - 214f, ry, 60f, 24f), "-1", _buttonSmall)) { Game.SpendItem(it.Id, 1); RefreshSnapshots(true); }
                if (GUI.Button(new Rect(colW - 150f, ry, 60f, 24f), T("删除", "Del"), _buttonSmall)) { Game.RemoveItem(it.Id); RefreshSnapshots(true); }
                if (GUI.Button(new Rect(colW - 86f, ry, 60f, 24f), T("加1", "+1"), _buttonSmall)) { Game.AddItem(it.Id, 1); RefreshSnapshots(true); }
            }
            GUI.EndScrollView();

            float rx = x + colW + 16f;
            float rw = area.width - colW - 40f;
            GUI.Label(new Rect(rx, listTop - 26f, rw, 22f),
                T("物品库（点击添加）  显示 " + _filtered.Count + " 项", "Catalog (click to add) " + _filtered.Count), _header);

            var rightRect = new Rect(rx, listTop, rw, listH);
            GUI.Box(rightRect, GUIContent.none);
            var view2 = new Rect(0f, 0f, rw - 24f, Math.Max(listH, _filtered.Count * rowH + 6f));
            _scrollCat = GUI.BeginScrollView(rightRect, _scrollCat, view2);

            int start2 = Math.Max(0, (int)(_scrollCat.y / rowH) - 1);
            int end2 = Math.Min(_filtered.Count, start2 + (int)(listH / rowH) + 3);
            for (int i = start2; i < end2; i++)
            {
                var kv = _filtered[i];
                float ry = i * rowH;
                GUI.Label(new Rect(8f, ry + 3f, rw - 110f, 24f), Cut(kv.Value, 20) + "  [" + kv.Key + "]", _labelDim);
                if (GUI.Button(new Rect(rw - 96f, ry, 84f, 24f), T("添加", "Add"), _buttonSmall))
                {
                    int cnt = 1;
                    int.TryParse(_addCount, out cnt);
                    if (cnt < 1) cnt = 1;
                    bool ok = Game.AddItem(kv.Key, cnt);
                    _itemInfo = (ok ? T("已添加 ", "Added ") : T("添加失败 ", "Failed ")) + kv.Value + " x" + cnt;
                    RefreshSnapshots(true);
                }
            }
            GUI.EndScrollView();
        }

        private void RebuildFilter()
        {
            if (_lastSearch == _search) return;
            _lastSearch = _search;
            _filtered.Clear();
            var cat = Game.Catalog;
            string q = (_search ?? "").Trim();
            for (int i = 0; i < cat.Count; i++)
            {
                var kv = cat[i];
                if (q.Length == 0
                    || (kv.Value != null && kv.Value.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (kv.Key != null && kv.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _filtered.Add(kv);
                }
            }
            _scrollCat = Vector2.zero;
        }

        // ------------------------------------------------------------------
        // Tab 3: 怪物刷新
        // ------------------------------------------------------------------

        private void DrawMonster(Rect area)
        {
            RefreshSnapshots(false);

            float x = area.x + 20f, y = area.y + 14f;

            GUI.Label(new Rect(x, y, 120f, 24f), T("刷新时间倍率", "Respawn x"), _label);
            _multBuf = GUI.TextField(new Rect(x + 110f, y - 2f, 80f, 28f), _multBuf, _field);

            if (GUI.Button(new Rect(x + 200f, y - 2f, 140f, 28f), T("应用到怪物表", "Apply to table"), _buttonSmall))
            {
                if (float.TryParse(_multBuf, out float m) && m > 0f)
                {
                    int n = Game.ScaleEnemyRegenTimes(m);
                    _regenInfo = T("已改写 " + n + " 条怪物刷新时间 (x" + m + ")", "Updated " + n + " entries (x" + m + ")");
                    RefreshSnapshots(true);
                }
                else _regenInfo = T("倍率无效", "Invalid multiplier");
            }
            if (GUI.Button(new Rect(x + 350f, y - 2f, 150f, 28f), T("恢复原始时间", "Restore original"), _buttonSmall))
            {
                int n = Game.RestoreEnemyRegenTimes();
                _regenInfo = T("已恢复 " + n + " 条", "Restored " + n);
                RefreshSnapshots(true);
            }

            float y2 = y + 44f;
            GUI.Label(new Rect(x, y2, 110f, 24f), T("剩余时间设为", "Set remaining"), _label);
            _remainBuf = GUI.TextField(new Rect(x + 126f, y2 - 2f, 80f, 28f), _remainBuf, _field);
            if (GUI.Button(new Rect(x + 216f, y2 - 2f, 110f, 28f), T("设为该值", "Set"), _buttonSmall))
            {
                if (int.TryParse(_remainBuf, out int v) && v >= 0)
                {
                    int n = Game.SetAllRegenRemained(v);
                    _regenInfo = T("已设置 " + n + " 条待刷新条目", "Set " + n + " entries");
                    RefreshSnapshots(true);
                }
            }
            if (GUI.Button(new Rect(x + 336f, y2 - 2f, 170f, 28f), T("立即刷新全部怪物", "Respawn all now"), _buttonSmall))
            {
                int n = Game.RefreshAllRegen();
                _regenInfo = T("已刷新 " + n + " 条（回城或切换地图后生效）", "Refreshed " + n + " entries");
                RefreshSnapshots(true);
            }

            if (!string.IsNullOrEmpty(_regenInfo))
                GUI.Label(new Rect(x + 540f, y2, 420f, 24f), _regenInfo, _labelDim);

            float listTop = area.y + 124f;
            float listH = area.height - 140f;
            float colW = area.width * 0.52f;

            GUI.Label(new Rect(x, listTop - 24f, colW, 22f),
                T("待刷新队列 (" + _regen.Count + ")", "Pending respawns (" + _regen.Count + ")"), _header);
            var leftRect = new Rect(x, listTop, colW, listH);
            GUI.Box(leftRect, GUIContent.none);

            float rowH = 28f;
            var view = new Rect(0f, 0f, colW - 24f, Math.Max(listH, _regen.Count * rowH + 6f));
            _scrollRegen = GUI.BeginScrollView(leftRect, _scrollRegen, view);
            int start = Math.Max(0, (int)(_scrollRegen.y / rowH) - 1);
            int end = Math.Min(_regen.Count, start + (int)(listH / rowH) + 3);
            for (int i = start; i < end; i++)
            {
                var rg = _regen[i];
                float ry = i * rowH;
                string title = string.IsNullOrEmpty(rg.EnemyNames) ? rg.Id : rg.EnemyNames;
                int max = Math.Max(1, rg.TimeMax);
                int pct = Mathf.Clamp(Mathf.RoundToInt((1f - (float)rg.TimeRemained / max) * 100f), 0, 100);
                GUI.Label(new Rect(8f, ry + 3f, colW - 270f, 24f), Cut(title, 26), _label);
                GUI.Label(new Rect(colW - 254f, ry + 3f, 160f, 24f), rg.TimeRemained + " / " + rg.TimeMax + "  " + pct + "%", _labelDim);
                if (GUI.Button(new Rect(colW - 86f, ry, 70f, 24f), T("立即", "Now"), _buttonSmall))
                {
                    Game.RefreshOneRegen(rg.Id);
                    RefreshSnapshots(true);
                }
            }
            GUI.EndScrollView();

            float rx = x + colW + 16f;
            float rw = area.width - colW - 40f;
            GUI.Label(new Rect(rx, listTop - 26f, rw, 22f),
                T("怪物刷新时间表 (" + _enemies.Count + ")", "Monster respawn table (" + _enemies.Count + ")"), _header);
            var rightRect = new Rect(rx, listTop, rw, listH);
            GUI.Box(rightRect, GUIContent.none);

            var view2 = new Rect(0f, 0f, rw - 24f, Math.Max(listH, _enemies.Count * rowH + 6f));
            _scrollEnemy = GUI.BeginScrollView(rightRect, _scrollEnemy, view2);
            int s2 = Math.Max(0, (int)(_scrollEnemy.y / rowH) - 1);
            int e2 = Math.Min(_enemies.Count, s2 + (int)(listH / rowH) + 3);
            for (int i = s2; i < e2; i++)
            {
                var en = _enemies[i];
                float ry = i * rowH;
                GUI.Label(new Rect(8f, ry + 3f, rw - 130f, 24f), Cut(en.Name, 22), _labelDim);
                GUI.Label(new Rect(rw - 116f, ry + 3f, 100f, 24f), en.RegenTime.ToString(), _label);
            }
            GUI.EndScrollView();
        }

        // ------------------------------------------------------------------
        // Tab 4: 敌人属性
        // ------------------------------------------------------------------

        private void DrawEnemyStats(Rect area)
        {
            RefreshSnapshots(false);
            RebuildEnemyFilter();

            float x = area.x + 20f, y = area.y + 14f;

            GUI.Label(new Rect(x, y, 60f, 24f), T("搜索", "Search"), _label);
            string ns = GUI.TextField(new Rect(x + 46f, y - 2f, 236f, 28f), _esSearch, _field);
            if (ns != _esSearch) { _esSearch = ns; _lastEsSearch = null; }

            GUI.Label(new Rect(x + 298f, y, 96f, 24f), T("全局倍率", "Global x"), _label);
            _esMultBuf = GUI.TextField(new Rect(x + 386f, y - 2f, 66f, 28f), _esMultBuf, _field);

            if (GUI.Button(new Rect(x + 460f, y - 2f, 128f, 28f), T("应用到全部", "Apply to all"), _buttonSmall))
            {
                if (float.TryParse(_esMultBuf, out float m) && m > 0f)
                {
                    int n = Game.ScaleAllEnemyStats(m);
                    _esInfo = T("已改写 " + n + " 种敌人 (x" + m + ")", "Updated " + n + " enemies (x" + m + ")");
                    RefreshSnapshots(true);
                }
                else _esInfo = T("倍率无效", "Invalid multiplier");
            }
            if (GUI.Button(new Rect(x + 596f, y - 2f, 140f, 28f), T("恢复全部敌人", "Restore all"), _buttonSmall))
            {
                int n = Game.RestoreAllEnemyStats();
                _esInfo = T("已恢复 " + n + " 种", "Restored " + n);
                _esSel = "";
                RefreshSnapshots(true);
            }

            float y2 = y + 38f;
            GUI.Label(new Rect(x, y2, 360f, 22f),
                T("已改动 ", "Edited ") + Game.EnemyEditedCount + T(" 种   /   覆盖 ", " / overrides ") + Game.EnemyOverrideCount + T(" 种", ""),
                _labelDim);
            if (!string.IsNullOrEmpty(_esInfo))
                GUI.Label(new Rect(x, y2 + 21f, 700f, 22f), _esInfo, _labelDim);

            float listTop = area.y + 92f;
            float listH = area.height - 110f;
            float colW = 400f;
            float rowH = 28f;

            var leftRect = new Rect(x, listTop, colW, listH);
            GUI.Box(leftRect, GUIContent.none);
            var view = new Rect(0f, 0f, colW - 24f, Math.Max(listH, _esFiltered.Count * rowH + 6f));
            _scrollEs = GUI.BeginScrollView(leftRect, _scrollEs, view);

            int start = Math.Max(0, (int)(_scrollEs.y / rowH) - 1);
            int end = Math.Min(_esFiltered.Count, start + (int)(listH / rowH) + 3);
            for (int i = start; i < end; i++)
            {
                var row = _esFiltered[i];
                float ry = i * rowH;
                var prev = GUI.backgroundColor;
                if (row.Id == _esSel) GUI.backgroundColor = new Color(0.35f, 0.62f, 0.95f);
                else if (row.Edited) GUI.backgroundColor = new Color(0.95f, 0.82f, 0.45f);

                string label = Cut(row.Name, 12) + "  HP " + row.MaxHp + "  STR " + row.Str
                             + "  DEF " + row.Def + "  AGI " + row.Agt;
                if (GUI.Button(new Rect(6f, ry, colW - 12f, 24f), label, _buttonSmall))
                    SelectEnemy(row.Id);
                GUI.backgroundColor = prev;
            }
            GUI.EndScrollView();

            float rx = x + colW + 20f;
            float rw = area.width - colW - 56f;

            int selIdx = FindEnemyIndex(_esSel);
            if (selIdx < 0)
            {
                GUI.Label(new Rect(rx, listTop + 10f, rw, 24f), T("← 先在左边选一种敌人", "← pick an enemy on the left"), _labelDim);
                DrawEnemyTips(rx, listTop + 54f);
                return;
            }
            var sel = _esRows[selIdx];

            // 场上实例数量（限流，避免每帧全场景扫描）
            if (Time.unscaledTime - _lastLiveCheck > 0.75f)
            {
                _lastLiveCheck = Time.unscaledTime;
                try { _esLive = Game.CountLiveEnemies(sel.Id); } catch { }
            }

            GUI.Label(new Rect(rx, listTop, rw, 26f), Cut(sel.Name, 26), _header);
            GUI.Label(new Rect(rx, listTop + 24f, rw, 20f),
                "id: " + sel.Id + T("    场上实例: ", "    live: ") + _esLive, _labelDim);

            float fy = listTop + 56f;
            float rh = 31f;
            float c1 = rx, c2 = rx + 244f;

            DrawEnemyField(c1, fy, T("生命上限", "Max HP"), ref _esHp);
            DrawEnemyField(c1, fy + rh, T("力量", "STR"), ref _esStr);
            DrawEnemyField(c1, fy + rh * 2, T("魔力", "MP"), ref _esSp);
            DrawEnemyField(c1, fy + rh * 3, T("防御", "DEF"), ref _esDef);
            DrawEnemyField(c1, fy + rh * 4, T("敏捷", "AGI"), ref _esAgt);

            DrawEnemyEnumField(c2, fy, T("防御属性", "Body"), ref _esBodyType, Game.BodyTypeNames);
            DrawEnemyField(c2, fy + rh, T("物理抗性", "Phys res"), ref _esDefPhys);
            DrawEnemyField(c2, fy + rh * 2, T("魔法抗性", "Magic res"), ref _esDefMagic);
            DrawEnemyEnumField(c2, fy + rh * 3, T("技能属性", "Skill"), ref _esSkillType, Game.SkillTypeNames);
            DrawEnemyField(c2, fy + rh * 4, T("大小", "Size"), ref _esBodySize);

            float by = fy + rh * 5 + 8f;
            if (GUI.Button(new Rect(rx, by, 130f, 32f), T("应用", "Apply"), _button))
                ApplySelectedEnemy();
            if (GUI.Button(new Rect(rx + 142f, by, 160f, 32f), T("恢复该敌人", "Restore"), _button))
            {
                bool ok = Game.RestoreEnemyStats(sel.Id);
                _esInfo = ok ? T("已恢复：" + sel.Name, "Restored: " + sel.Name) : T("没有可恢复的原始值", "No original value");
                RefreshSnapshots(true);
                SelectEnemy(sel.Id);
            }

            by += 40f;
            if (GUI.Button(new Rect(rx, by, 220f, 28f), T("立即同步到当前战斗", "Sync to current battle"), _buttonSmall))
            {
                int n = Game.ApplyEnemyOverridesToBattle();
                _esInfo = T("已同步 " + n + " 个场上实例", "Synced " + n + " instances");
            }

            DrawEnemyTips(rx, by + 40f);
        }

        private void DrawEnemyTips(float x, float y)
        {
            GUI.Label(new Rect(x, y, 520f, 150f),
                T("说明：\n· 改的是敌人数据表，对所有同类敌人生效；已在场的敌人 1 秒内自动套用（残血按比例）。\n· 防御属性可选 NONE / TREE / STONE / STRONGATPHYSICAL / STRONGATMAGIC / FIRE /\n  THUNDER / ICE / IRON / WATER；技能属性与游戏枚举一致。\n· 物理抗性 / 魔法抗性是小数（例如 0.5 = 50%）。\n· 改动只在本次运行的内存中，重开游戏即恢复。",
                  "Notes:\n- Edits the enemy data table for that enemy type; live instances follow within 1s (HP ratio kept).\n- Body type: NONE/TREE/STONE/STRONGATPHYSICAL/STRONGATMAGIC/FIRE/THUNDER/ICE/IRON/WATER.\n- Physical / magic resistance are floats (0.5 = 50%).\n- Changes last until you restart the game."), _labelDim);
        }

        private float DrawEnemyField(float x, float y, string label, ref string buf)
        {
            GUI.Label(new Rect(x, y + 4f, 92f, 24f), label, _label);
            string next = GUI.TextField(new Rect(x + 96f, y, 78f, 26f), buf, _field);
            if (next != buf) buf = next;
            return y + 31f;
        }

        private void DrawEnemyEnumField(float x, float y, string label, ref string buf, string[] names)
        {
            GUI.Label(new Rect(x, y + 4f, 92f, 24f), label, _label);
            string next = GUI.TextField(new Rect(x + 96f, y, 56f, 26f), buf, _field);
            if (next != buf) buf = next;

            string nm = "-";
            if (int.TryParse(buf, out int v) && v >= 0 && v < names.Length) nm = names[v];
            GUI.Label(new Rect(x + 158f, y + 4f, 86f, 24f), Cut(nm, 14), _labelDim);
        }

        private void SelectEnemy(string id)
        {
            int idx = FindEnemyIndex(id);
            if (idx < 0) return;
            var row = _esRows[idx];
            _esSel = row.Id;
            _esHp = row.MaxHp.ToString();
            _esStr = row.Str.ToString();
            _esSp = row.Sp.ToString();
            _esDef = row.Def.ToString();
            _esAgt = row.Agt.ToString();
            _esBodySize = row.BodySize.ToString();
            _esDefPhys = row.DefRatePhysical.ToString("0.###");
            _esDefMagic = row.DefRateMagic.ToString("0.###");
            _esBodyType = row.BodyType.ToString();
            _esSkillType = row.SkillType.ToString();
            _esLive = 0;
            _lastLiveCheck = -99f;
        }

        private void ApplySelectedEnemy()
        {
            if (string.IsNullOrEmpty(_esSel)) return;
            if (!int.TryParse(_esHp, out int hp) || !int.TryParse(_esStr, out int str)
                || !int.TryParse(_esSp, out int sp) || !int.TryParse(_esDef, out int def)
                || !int.TryParse(_esAgt, out int agt) || !int.TryParse(_esBodySize, out int size)
                || !int.TryParse(_esBodyType, out int body) || !int.TryParse(_esSkillType, out int skill)
                || !float.TryParse(_esDefPhys, out float phys) || !float.TryParse(_esDefMagic, out float magic))
            {
                _esInfo = T("请输入数字（抗性可带小数）", "Numbers only");
                return;
            }

            var v = new Game.EnemyStatRow
            {
                Id = _esSel,
                MaxHp = hp,
                Str = str,
                Sp = sp,
                Def = def,
                Agt = agt,
                BodySize = size,
                BodyType = body,
                SkillType = skill,
                DefRatePhysical = phys,
                DefRateMagic = magic,
            };

            bool ok = Game.SetEnemyStats(v);
            _esInfo = ok ? T("已应用", "Applied") : T("应用失败", "Failed");
            RefreshSnapshots(true);
            SelectEnemy(_esSel);
        }

        private int FindEnemyIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _esRows.Count; i++)
                if (_esRows[i].Id == id) return i;
            return -1;
        }

        private void RebuildEnemyFilter()
        {
            // 注意：快照每 0.75 秒会换一批行对象，这里必须重建列表，
            // 但只有"搜索词变了"才把滚动条归零，否则用户往下翻会被一直弹回顶部。
            bool searchChanged = _lastEsSearch != _esSearch;
            if (!searchChanged && ReferenceEquals(_esFilteredRef, _esRows)) return;

            _lastEsSearch = _esSearch;
            _esFilteredRef = _esRows;
            _esFiltered.Clear();
            string q = (_esSearch ?? "").Trim();
            for (int i = 0; i < _esRows.Count; i++)
            {
                var r = _esRows[i];
                if (q.Length == 0
                    || (r.Name != null && r.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (r.Id != null && r.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _esFiltered.Add(r);
                }
            }
            if (searchChanged) _scrollEs = Vector2.zero;
        }

        // ------------------------------------------------------------------
        // 样式
        // ------------------------------------------------------------------

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            try
            {
                _font = LoadCjkFont();
                _cjkOk = _font != null;
            }
            catch { _font = null; _cjkOk = false; }

            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = false };
            _label.normal.textColor = new Color(0.92f, 0.93f, 0.95f);
            _labelDim = new GUIStyle(_label) { fontSize = 14 };
            _labelDim.normal.textColor = new Color(0.66f, 0.70f, 0.78f);
            _title = new GUIStyle(_label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _header = new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(0.62f, 0.82f, 1f);
            _warn = new GUIStyle(_label) { fontSize = 16 };
            _warn.normal.textColor = new Color(1f, 0.78f, 0.35f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            _buttonSmall = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = 14 };
            _tabOn = new GUIStyle(_button);

            if (_font != null)
            {
                var arr = new GUIStyle[] { _label, _labelDim, _title, _header, _warn, _button, _buttonSmall, _field };
                for (int i = 0; i < arr.Length; i++)
                {
                    try { arr[i].font = _font; } catch { }
                }
            }
        }

        private Font LoadCjkFont()
        {
            string[] candidates = { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun" };
            foreach (var name in candidates)
            {
                try
                {
                    var f = Font.CreateDynamicFontFromOSFont(name, 16);
                    if (f != null && f.Pointer != IntPtr.Zero && f.HasCharacter('中'))
                    {
                        Plugin.LogSource?.LogInfo("[字体] 系统动态字体: " + name);
                        return f;
                    }
                }
                catch { }
            }

            // 借用游戏自己加载的中文字体资源
            try
            {
                var fontType = Il2CppInterop.Runtime.Il2CppType.Of<Font>();
                var objs = Resources.FindObjectsOfTypeAll(fontType);
                if (objs != null)
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < objs.Length; i++)
                    {
                        Font f = null;
                        try { f = objs[i]?.TryCast<Font>(); } catch { }
                        if (f == null || f.Pointer == IntPtr.Zero) continue;

                        bool cjk = false;
                        try { cjk = f.HasCharacter('中'); } catch { }
                        if (sb.Length < 400) sb.Append(f.name).Append(cjk ? "(中)|" : "|");
                        if (cjk)
                        {
                            Plugin.LogSource?.LogInfo("[字体] 复用游戏字体: " + f.name);
                            return f;
                        }
                    }
                    Plugin.LogSource?.LogInfo("[字体] 无中文字体，Font 资源: " + sb);
                }
            }
            catch (Exception e)
            {
                Plugin.LogSource?.LogWarning("[字体] 查找游戏字体失败: " + e.GetType().Name + " " + e.Message);
            }
            return null;
        }
    }
}
