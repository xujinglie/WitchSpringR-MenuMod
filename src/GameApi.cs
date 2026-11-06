using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;

using WS1RCLASS;

namespace WSMenuMod
{
    /// <summary>
    /// 游戏内数据访问层。所有调用都做了空值 / 异常保护，
    /// 因为菜单可能在标题画面、场景切换、战斗中等各种时机被打开。
    /// </summary>
    internal static class Game
    {
        public static bool IsNull(Il2CppObjectBase o)
        {
            if (o is null) return true;
            try { return o.Pointer == IntPtr.Zero; } catch { return true; }
        }

        public static SystemOperator SO
        {
            get
            {
                try
                {
                    var so = SystemOperator.instance;
                    return IsNull(so) ? null : so;
                }
                catch { return null; }
            }
        }

        public static DataSet DS
        {
            get
            {
                var so = SO;
                if (so == null) return null;
                try
                {
                    var ds = so.dataSet;
                    return IsNull(ds) ? null : ds;
                }
                catch { return null; }
            }
        }

        public static PlayerStat PS
        {
            get
            {
                var ds = DS;
                if (ds == null) return null;
                try
                {
                    var ps = ds.playerStat;
                    return IsNull(ps) ? null : ps;
                }
                catch { return null; }
            }
        }

        /// <summary>数据是否可用（读档完成、进入游戏后）。</summary>
        public static bool Ready => PS != null;

        /// <summary>当前场景名（用于判断是否在游戏内）。</summary>
        public static string SceneName
        {
            get
            {
                try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? ""; }
                catch { return ""; }
            }
        }

        /// <summary>当前是否真的在游戏内（标题/Logo 画面里 dataSet 也存在，但只是占位数据）。</summary>
        public static bool InGame
        {
            get
            {
                if (PS == null) return false;
                string s = SceneName;
                if (string.IsNullOrEmpty(s)) return false;
                if (s == "#Title" || s == "#Logo") return false;
                return true;
            }
        }

        /// <summary>诊断输出（F9 触发），写进 BepInEx 日志。</summary>
        public static void DumpDiagnostics()
        {
            var log = Plugin.LogSource;
            var ds = DS;
            var ps = PS;
            if (log == null) return;
            log.LogInfo("===== 诊断 =====");
            log.LogInfo("场景=" + SceneName);
            if (ds == null) { log.LogInfo("DataSet 为空"); return; }
            if (ps == null) { log.LogInfo("PlayerStat 为空"); return; }

            int listCount = 0;
            try
            {
                var list = ps.myItemList;
                if (!IsNull(list))
                {
                    listCount = list.Count;
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < list.Count && i < 40; i++)
                    {
                        try { var it = list[i]; if (!IsNull(it)) sb.Append(it.id).Append("x").Append(it.count).Append(" "); } catch { }
                    }
                    log.LogInfo("myItemList.Count=" + listCount + " 前40项: " + sb);
                }
            }
            catch (Exception e) { log.LogWarning("读取 myItemList 失败: " + e.Message); }

            try { log.LogInfo("GetItemCount(DryLeaf)=" + ds.GetItemCount("DryLeaf")); } catch (Exception e) { log.LogWarning("GetItemCount 失败: " + e.Message); }
            try { log.LogInfo("GetItemInfo(DryLeaf) 非空=" + !IsNull(ds.GetItemInfo("DryLeaf"))); } catch (Exception e) { log.LogWarning("GetItemInfo 失败: " + e.Message); }
            try { log.LogInfo("CheckIsOnItemList(DryLeaf)=" + ds.CheckIsOnItemList("DryLeaf")); } catch (Exception e) { }
            try { log.LogInfo("itemInfoList.Count=" + ds.itemInfoList.Count); } catch { }
            try { log.LogInfo("enemyList.dataArray.Length=" + ds.enemyList.dataArray.Length); } catch { }
            try { log.LogInfo("timeStat.regenList.Count=" + ds.timeStat.regenList.Count); } catch { }
            try { log.LogInfo("enemyList 前5项: " + DescribeEnemyTable(5)); } catch (Exception e) { log.LogWarning("读取 enemyList 失败: " + e.Message); }
            try { log.LogInfo("场上 EnemyBasic: " + DescribeLiveEnemies(8)); } catch (Exception e) { log.LogWarning("扫描场上敌人失败: " + e.Message); }
            log.LogInfo("=================");
        }

        private static string DescribeEnemyTable(int max)
        {
            var ds = DS;
            if (ds == null) return "无";
            var sb = new System.Text.StringBuilder();
            var arr = ds.enemyList?.dataArray;
            if (arr == null) return "无";
            int n = 0;
            for (int i = 0; i < arr.Length && n < max; i++)
            {
                var ed = arr[i];
                if (IsNull(ed)) continue;
                sb.Append(ed.id).Append("[hp=").Append(ed.maxhp).Append(",atk=").Append(ed.str)
                  .Append(",def=").Append(ed.def).Append(",agt=").Append(ed.agt).Append("] ");
                n++;
            }
            return sb.ToString();
        }

        private static string DescribeLiveEnemies(int max)
        {
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var eb in AllLiveEnemies())
            {
                if (n >= max) break;
                string id = "?";
                try { id = eb.enemyID; } catch { }
                string st = "status=null";
                try
                {
                    var s = eb.status;
                    if (s != null && s.Pointer != IntPtr.Zero)
                        st = "hp=" + s.hp + "/" + s.maxHP + ",atk=" + s.power + ",def=" + s.def + ",agt=" + s.agt;
                }
                catch { }
                sb.Append(id).Append("(").Append(st).Append(") ");
                n++;
            }
            return n == 0 ? "0 个" : sb.ToString();
        }

        // ------------------------------------------------------------------
        // 角色属性
        // ------------------------------------------------------------------

        internal sealed class StatRow
        {
            public string Key;
            public string Zh;
            public string En;
            public Func<PlayerStat, int> Get;
            public Action<PlayerStat, int> Set;
            public bool IsCurrent;   // hp / mp 这类"当前值"字段
        }

        public static readonly StatRow[] StatRows =
        {
            new StatRow { Key = "maxHp", Zh = "生命上限", En = "Max HP",   Get = p => p.maxHp, Set = (p, v) => p.maxHp = v },
            new StatRow { Key = "hp",    Zh = "当前生命", En = "HP",       Get = p => p.hp,    Set = (p, v) => p.hp = v, IsCurrent = true },
            new StatRow { Key = "maxMp", Zh = "魔力上限", En = "Max MP",   Get = p => p.maxMp, Set = (p, v) => p.maxMp = v },
            new StatRow { Key = "mp",    Zh = "当前魔力", En = "MP",       Get = p => p.mp,    Set = (p, v) => p.mp = v, IsCurrent = true },
            new StatRow { Key = "str",   Zh = "力量",     En = "STR",      Get = p => p.str,   Set = (p, v) => p.str = v },
            new StatRow { Key = "def",   Zh = "防御",     En = "DEF",      Get = p => p.def,   Set = (p, v) => p.def = v },
            new StatRow { Key = "agt",   Zh = "敏捷",     En = "AGT",      Get = p => p.agt,   Set = (p, v) => p.agt = v },
            new StatRow { Key = "sp",    Zh = "技能点",   En = "SP",       Get = p => p.sp,    Set = (p, v) => p.sp = v },
            new StatRow { Key = "fame",  Zh = "名气",     En = "Fame",     Get = p => p.fame,  Set = (p, v) => p.fame = v },
            new StatRow { Key = "battleEXP",   Zh = "战斗经验", En = "Battle EXP", Get = p => p.battleEXP,   Set = (p, v) => p.battleEXP = v },
            new StatRow { Key = "bodyLevel",   Zh = "体质等级", En = "Body Lv",    Get = p => p.bodyLevel,   Set = (p, v) => p.bodyLevel = v },
            new StatRow { Key = "mindLevel",   Zh = "精神等级", En = "Mind Lv",    Get = p => p.mindLevel,   Set = (p, v) => p.mindLevel = v },
        };

        public static int GetGold()
        {
            var ps = PS;
            return ps == null ? 0 : ps.gold;
        }

        public static bool SetGold(int value)
        {
            var ps = PS;
            if (ps == null) return false;
            ps.gold = Math.Max(0, value);
            Plugin.LogSource?.LogInfo("[属性] 金币设为 " + ps.gold);
            RefreshHud();
            return true;
        }

        /// <summary>读取游戏内部计算后的实际数值（含装备加成）。失败时返回 0。</summary>
        public static int GetComputed(string what)
        {
            var ds = DS;
            if (ds == null) return 0;
            try
            {
                var sc = ds.sc;
                if (IsNull(sc)) return 0;
                switch (what)
                {
                    case "maxHp": return sc.GetMaxHP();
                    case "maxMp": return sc.GetMaxMP();
                    case "str": return sc.GetSTR();
                    case "def": return sc.GetDEF();
                    case "agt": return sc.GetAGT();
                    case "sp": return sc.GetSP();
                    case "level": return sc.GetBattleLevel();
                }
            }
            catch { }
            return 0;
        }

        /// <summary>刷新游戏 HUD（血条 / 蓝条等）。</summary>
        public static void RefreshHud()
        {
            var ds = DS;
            if (ds == null) return;
            try
            {
                var ui = ds.ui;
                if (!IsNull(ui)) ui.SetBar();
            }
            catch { }
        }

        public static void HealFull()
        {
            var ps = PS;
            if (ps == null) return;
            int maxHp = ps.maxHp, maxMp = ps.maxMp;
            if (maxHp < 1) maxHp = 1;
            if (maxMp < 1) maxMp = 1;
            ps.hp = maxHp;
            ps.mp = maxMp;
            RefreshHud();
        }

        public static void BumpMainStats(int delta)
        {
            var ps = PS;
            if (ps == null) return;
            ps.str = Math.Max(0, ps.str + delta);
            ps.def = Math.Max(0, ps.def + delta);
            ps.agt = Math.Max(0, ps.agt + delta);
            RefreshHud();
        }

        // ------------------------------------------------------------------
        // 背包
        // ------------------------------------------------------------------

        internal struct ItemRow
        {
            public string Id;
            public int Count;
            public int Category;
        }

        public static List<ItemRow> SnapshotInventory()
        {
            var rows = new List<ItemRow>();
            var ps = PS;
            if (ps == null) return rows;
            try
            {
                var list = ps.myItemList;
                if (IsNull(list)) return rows;
                int n = list.Count;
                for (int i = 0; i < n; i++)
                {
                    try
                    {
                        var it = list[i];
                        if (IsNull(it)) continue;
                        rows.Add(new ItemRow { Id = it.id, Count = it.count, Category = (int)it.category });
                    }
                    catch { }
                }
            }
            catch { }
            return rows;
        }

        public static string ItemName(string id)
        {
            var ds = DS;
            if (ds == null || string.IsNullOrEmpty(id)) return id ?? "";
            try
            {
                var s = ds.GetItemName(id);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            catch { }
            return id;
        }

        public static int ItemCount(string id)
        {
            var ds = DS;
            if (ds == null) return 0;
            try { return ds.GetItemCount(id); } catch { return 0; }
        }

        // ---- 直接操作背包列表（ps.myItemList）：游戏的 AddItem/SpendItem 在本作里
        // ---- 大多数情况下会返回 null 或不起作用，所以以背包列表为准，并校验结果。

        private static Il2CppSystem.Collections.Generic.List<Item> Bag()
        {
            var ps = PS;
            if (ps == null) return null;
            try
            {
                var list = ps.myItemList;
                return IsNull(list) ? null : list;
            }
            catch { return null; }
        }

        /// <summary>背包里某个物品的实际数量（直接扫 myItemList）。</summary>
        public static int BagCount(string id)
        {
            var list = Bag();
            if (list == null || string.IsNullOrEmpty(id)) return 0;
            int sum = 0;
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var it = list[i];
                    if (IsNull(it)) continue;
                    if (it.id == id) sum += it.count;
                }
            }
            catch { }
            return sum;
        }

        private static bool InsertIntoBag(string id, int count, out string how)
        {
            how = "";
            var list = Bag();
            if (list == null) { how = "背包不可用"; return false; }

            // 已有同 id 条目就直接叠加
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                if (IsNull(it) || it.id != id) continue;
                it.count += count;
                how = "叠加";
                return true;
            }

            // 新建条目
            ItemCategory cat = 0;
            try
            {
                var ds = DS;
                var info = ds == null ? null : ds.GetItemInfo(id);
                if (info != null && !IsNull(info)) cat = info.category;
            }
            catch { }

            var item = new Item();
            item.id = id;
            item.count = count;
            item.category = cat;
            list.Add(item);
            how = "新建条目";
            return true;
        }

        private static bool DeleteFromBag(string id, int maxCount)
        {
            var list = Bag();
            if (list == null) return false;
            bool touched = false;
            try
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var it = list[i];
                    if (IsNull(it) || it.id != id) continue;
                    if (maxCount <= 0 || it.count <= maxCount)
                    {
                        list.RemoveAt(i);
                        touched = true;
                    }
                    else
                    {
                        it.count -= maxCount;
                        touched = true;
                    }
                    if (maxCount > 0) break;
                }
            }
            catch { }
            return touched;
        }

        /// <summary>添加物品。</summary>
        public static bool AddItem(string id, int count)
        {
            var ds = DS;
            if (ds == null || string.IsNullOrEmpty(id) || count <= 0) return false;

            int before = BagCount(id);

            // 先让游戏自己处理（任务、图鉴、成就等记账）
            try
            {
                var it = ds.AddItem(id, count, GetItemType.EVENT);
                if (!IsNull(it) || BagCount(id) > before)
                {
                    Plugin.LogSource?.LogInfo("[物品] 添加 " + id + " x" + count + " 成功 (AddItem)");
                    return true;
                }
            }
            catch { }
            try
            {
                ds.AddUseItem(id, count);
                if (BagCount(id) > before)
                {
                    Plugin.LogSource?.LogInfo("[物品] 添加 " + id + " x" + count + " 成功 (AddUseItem)");
                    return true;
                }
            }
            catch { }

            // 兜底：直接写入背包列表
            if (InsertIntoBag(id, count, out string how))
            {
                Plugin.LogSource?.LogInfo("[物品] 添加 " + id + " x" + count + " 成功 (直接写入/" + how + ")");
                return true;
            }
            Plugin.LogSource?.LogWarning("[物品] 添加失败 " + id + " x" + count + "（" + how + "）");
            return false;
        }

        /// <summary>扣除一定数量（数量归零时移除条目）。</summary>
        public static bool SpendItem(string id, int count)
        {
            var ds = DS;
            if (ds == null || string.IsNullOrEmpty(id) || count <= 0) return false;

            int before = BagCount(id);
            try
            {
                ds.SpendItem(id, count);
                if (BagCount(id) < before) { Plugin.LogSource?.LogInfo("[物品] 扣除 " + id + " x" + count + " (SpendItem)"); return true; }
            }
            catch { }

            if (DeleteFromBag(id, count))
            {
                Plugin.LogSource?.LogInfo("[物品] 扣除 " + id + " x" + count + " (直接扣除)");
                return true;
            }
            return false;
        }

        /// <summary>整条移除。</summary>
        public static bool RemoveItem(string id)
        {
            var ds = DS;
            if (ds == null || string.IsNullOrEmpty(id)) return false;

            try
            {
                ds.RemoveItem(id);
                if (BagCount(id) == 0) { Plugin.LogSource?.LogInfo("[物品] 移除 " + id + " (RemoveItem)"); return true; }
            }
            catch { }

            if (DeleteFromBag(id, 0) && BagCount(id) == 0)
            {
                Plugin.LogSource?.LogInfo("[物品] 移除 " + id + " (直接移除)");
                return true;
            }
            return false;
        }

        // 物品库（id -> 显示名），懒加载
        private static List<KeyValuePair<string, string>> _catalog;
        public static int CatalogCount => _catalog?.Count ?? 0;

        public static void ClearCatalog() => _catalog = null;

        public static void EnsureCatalog()
        {
            if (_catalog != null) return;
            var ds = DS;
            var tmp = new List<KeyValuePair<string, string>>();
            if (ds != null)
            {
                try
                {
                    var infos = ds.itemInfoList;
                    if (!IsNull(infos))
                    {
                        int n = infos.Count;
                        for (int i = 0; i < n; i++)
                        {
                            string id = null;
                            try { id = infos[i]?.id; } catch { }
                            if (string.IsNullOrEmpty(id)) continue;
                            tmp.Add(new KeyValuePair<string, string>(id, ItemName(id)));
                        }
                    }
                }
                catch { }
            }
            _catalog = tmp;
        }

        public static List<KeyValuePair<string, string>> Catalog => _catalog ?? new List<KeyValuePair<string, string>>();

        // ------------------------------------------------------------------
        // 怪物刷新
        // ------------------------------------------------------------------

        internal sealed class RegenRow
        {
            public string Id;
            public string Category;
            public int TimeMax;
            public int TimeRemained;
            public string EnemyNames;
        }

        public static List<RegenRow> SnapshotRegen()
        {
            var rows = new List<RegenRow>();
            var ds = DS;
            if (ds == null) return rows;
            try
            {
                var ts = ds.timeStat;
                if (IsNull(ts)) return rows;
                var list = ts.regenList;
                if (IsNull(list)) return rows;
                int n = list.Count;
                for (int i = 0; i < n; i++)
                {
                    try
                    {
                        var rc = list[i];
                        if (IsNull(rc)) continue;
                        rows.Add(new RegenRow
                        {
                            Id = rc.id,
                            Category = rc.category,
                            TimeMax = rc.time_max,
                            TimeRemained = rc.time_remained,
                            EnemyNames = DescribeRegen(rc.id),
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return rows;
        }

        /// <summary>把刷新条目的 id 翻译成怪物名字（战斗类刷新）。</summary>
        private static string DescribeRegen(string regenId)
        {
            var ds = DS;
            if (ds == null || string.IsNullOrEmpty(regenId)) return "";
            try
            {
                var psys = ds.playSystem;
                if (IsNull(psys)) return "";
                var bl = psys.battleList_regen;
                if (IsNull(bl)) return "";
                for (int i = 0; i < bl.Count; i++)
                {
                    var bi = bl[i];
                    if (IsNull(bi) || bi.id != regenId) continue;
                    var names = new List<string>();
                    var enemys = bi.enemys;
                    if (!IsNull(enemys))
                    {
                        for (int j = 0; j < enemys.Count; j++)
                        {
                            string eid = enemys[j];
                            names.Add(EnemyName(eid));
                        }
                    }
                    return string.Join(", ", names);
                }
            }
            catch { }
            return "";
        }

        public static string EnemyName(string enemyId)
        {
            var ds = DS;
            if (ds == null || string.IsNullOrEmpty(enemyId)) return enemyId ?? "";
            try
            {
                var ed = ds.GetEnemyData(enemyId);
                if (!IsNull(ed) && !string.IsNullOrEmpty(ed.nameid))
                {
                    var nm = ds.GetName(ed.nameid);
                    if (!string.IsNullOrEmpty(nm)) return nm;
                }
            }
            catch { }
            return enemyId;
        }

        /// <summary>把所有待刷新条目的剩余时间清零并刷新（= 立刻刷新全部怪物）。</summary>
        public static int RefreshAllRegen()
        {
            var ds = DS;
            if (ds == null) return 0;
            int changed = 0;
            try
            {
                var ts = ds.timeStat;
                if (IsNull(ts)) return 0;
                var list = ts.regenList;
                if (IsNull(list)) return 0;
                for (int i = 0; i < list.Count; i++)
                {
                    try
                    {
                        var rc = list[i];
                        if (IsNull(rc)) continue;
                        if (rc.time_remained != 0) { rc.time_remained = 0; changed++; }
                    }
                    catch { }
                }
                ds.RefreshBattleRegenList();
            }
            catch { }
            Plugin.LogSource?.LogInfo("[刷新] 立即刷新全部，改动条目 " + changed);
            return changed;
        }

        public static bool RefreshOneRegen(string regenId)
        {
            var ds = DS;
            if (ds == null) return false;
            try
            {
                var ts = ds.timeStat;
                if (IsNull(ts)) return false;
                var list = ts.regenList;
                if (IsNull(list)) return false;
                for (int i = 0; i < list.Count; i++)
                {
                    var rc = list[i];
                    if (IsNull(rc) || rc.id != regenId) continue;
                    rc.time_remained = 0;
                    ds.RefreshBattleRegenList();
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>把所有待刷新条目的剩余时间设为指定值。</summary>
        public static int SetAllRegenRemained(int value)
        {
            var ds = DS;
            if (ds == null) return 0;
            int changed = 0;
            try
            {
                var ts = ds.timeStat;
                if (IsNull(ts)) return 0;
                var list = ts.regenList;
                if (IsNull(list)) return 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var rc = list[i];
                    if (IsNull(rc)) continue;
                    rc.time_remained = value;
                    if (value > rc.time_max) rc.time_max = value;
                    changed++;
                }
                ds.RefreshBattleRegenList();
            }
            catch { }
            return changed;
        }

        // ---- 怪物刷新时间表（EnemyListData.regentime）----

        private static readonly Dictionary<string, int> _origRegenTime = new Dictionary<string, int>();

        internal struct EnemyRow
        {
            public string Id;
            public string Name;
            public int RegenTime;
        }

        public static List<EnemyRow> SnapshotEnemies()
        {
            var rows = new List<EnemyRow>();
            var ds = DS;
            if (ds == null) return rows;
            try
            {
                var el = ds.enemyList;
                if (IsNull(el)) return rows;
                var arr = el.dataArray;
                if (IsNull(arr)) return rows;
                int n = arr.Length;
                for (int i = 0; i < n; i++)
                {
                    try
                    {
                        var ed = arr[i];
                        if (IsNull(ed) || string.IsNullOrEmpty(ed.id)) continue;
                        rows.Add(new EnemyRow
                        {
                            Id = ed.id,
                            Name = EnemyName(ed.id),
                            RegenTime = ed.regentime,
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return rows;
        }

        /// <summary>按倍率改写怪物刷新时间表（只影响之后产生的刷新计时）。</summary>
        public static int ScaleEnemyRegenTimes(float multiplier)
        {
            var ds = DS;
            if (ds == null) return 0;
            if (multiplier < 0.01f) multiplier = 0.01f;
            int changed = 0;
            try
            {
                var el = ds.enemyList;
                if (IsNull(el)) return 0;
                var arr = el.dataArray;
                if (IsNull(arr)) return 0;
                for (int i = 0; i < arr.Length; i++)
                {
                    var ed = arr[i];
                    if (IsNull(ed) || string.IsNullOrEmpty(ed.id)) continue;
                    if (!_origRegenTime.ContainsKey(ed.id)) _origRegenTime[ed.id] = ed.regentime;
                    int origin = _origRegenTime[ed.id];
                    int next = (int)Math.Round(origin * multiplier);
                    if (next < 0) next = 0;
                    if (ed.regentime != next) { ed.regentime = next; changed++; }
                }
            }
            catch { }
            Plugin.LogSource?.LogInfo("[刷新] 怪物刷新时间表 x" + multiplier + "，改写 " + changed + " 条");
            return changed;
        }

        /// <summary>把刷新时间表恢复成进入游戏时的原始值。</summary>
        public static int RestoreEnemyRegenTimes()
        {
            var ds = DS;
            if (ds == null || _origRegenTime.Count == 0) return 0;
            int changed = 0;
            try
            {
                var el = ds.enemyList;
                if (IsNull(el)) return 0;
                var arr = el.dataArray;
                if (IsNull(arr)) return 0;
                for (int i = 0; i < arr.Length; i++)
                {
                    var ed = arr[i];
                    if (IsNull(ed) || string.IsNullOrEmpty(ed.id)) continue;
                    if (_origRegenTime.TryGetValue(ed.id, out int origin))
                    {
                        if (ed.regentime != origin) { ed.regentime = origin; changed++; }
                    }
                }
                _origRegenTime.Clear();
            }
            catch { }
            return changed;
        }

        // ------------------------------------------------------------------
        // 敌人属性（DataSet.enemyList.dataArray = EnemyListData）
        // 可改：生命 / 力量 / 魔力 / 防御 / 防御属性(BodyType) /
        //       物理抗性 / 魔法抗性 / 敏捷 / 技能属性(SkillType) / 大小
        // 数据表是"刷新出来的初始值"，已在场上的实例另用覆盖值同步。
        // ------------------------------------------------------------------

        internal sealed class EnemyStatRow
        {
            public string Id;
            public string Name;
            public int MaxHp;
            public int Str;
            public int Sp;
            public int Def;
            public int Agt;
            public int BodySize;
            public float DefRatePhysical;
            public float DefRateMagic;
            public int BodyType;
            public int SkillType;
            public bool Edited;

            public EnemyStatRow Clone() => (EnemyStatRow)MemberwiseClone();
        }

        // 取值顺序与游戏枚举一致（BodyType / SkillType）
        public static readonly string[] BodyTypeNames =
        {
            "NONE", "TREE", "STONE", "STRONGATPHYSICAL", "STRONGATMAGIC",
            "FIRE", "THUNDER", "ICE", "IRON", "WATER",
        };

        public static readonly string[] SkillTypeNames =
        {
            "NONE", "FIRE", "THUNDER", "ICE", "EXPLOSION", "DOUBLE", "SAVE", "CLEAR",
            "MINDCONTROL", "FAINT", "POISON", "MAGICSHADOW", "DARK", "POWERDOWN",
            "DEFENCEDOWN", "SLOW", "IGNOREDEFENCE", "SWORD", "REFLECT", "SHIELDREFLECT",
            "WATER", "STICKEXPLOSION", "STICKWAVE", "RECOVERDOWN",
        };

        public static string BodyTypeName(int v)
            => (v >= 0 && v < BodyTypeNames.Length) ? BodyTypeNames[v] : v.ToString();

        public static string SkillTypeName(int v)
            => (v >= 0 && v < SkillTypeNames.Length) ? SkillTypeNames[v] : v.ToString();

        // id -> 原始值（首次改动时记录）
        private static readonly Dictionary<string, EnemyStatRow> _enemyOrigStat = new Dictionary<string, EnemyStatRow>();
        // id -> 需要套用到场上实例的值
        private static readonly Dictionary<string, EnemyStatRow> _enemyOverride = new Dictionary<string, EnemyStatRow>();

        public static int EnemyOverrideCount => _enemyOverride.Count;
        public static int EnemyEditedCount => _enemyOrigStat.Count;

        private static EnemyListData FindEnemyData(string id)
        {
            var ds = DS;
            if (ds == null || string.IsNullOrEmpty(id)) return null;
            try
            {
                var el = ds.enemyList;
                if (IsNull(el)) return null;
                var arr = el.dataArray;
                if (IsNull(arr)) return null;
                for (int i = 0; i < arr.Length; i++)
                {
                    var ed = arr[i];
                    if (IsNull(ed)) continue;
                    if (ed.id == id) return ed;
                }
            }
            catch { }
            return null;
        }

        private static EnemyStatRow ReadRow(EnemyListData ed)
        {
            var r = new EnemyStatRow
            {
                Id = ed.id,
                Name = EnemyName(ed.id),
                MaxHp = ed.maxhp,
                Str = ed.str,
                Sp = ed.sp,
                Def = ed.def,
                Agt = ed.agt,
                BodySize = ed.bodysize,
                DefRatePhysical = ed.defratephysical,
                DefRateMagic = ed.defratemagic,
                BodyType = (int)ed.bodytype,
                SkillType = (int)ed.skilltype,
                Edited = _enemyOverride.ContainsKey(ed.id),
            };
            return r;
        }

        public static List<EnemyStatRow> SnapshotEnemyStats()
        {
            var rows = new List<EnemyStatRow>();
            var ds = DS;
            if (ds == null) return rows;
            try
            {
                var el = ds.enemyList;
                if (IsNull(el)) return rows;
                var arr = el.dataArray;
                if (IsNull(arr)) return rows;
                for (int i = 0; i < arr.Length; i++)
                {
                    try
                    {
                        var ed = arr[i];
                        if (IsNull(ed) || string.IsNullOrEmpty(ed.id)) continue;
                        rows.Add(ReadRow(ed));
                    }
                    catch { }
                }
            }
            catch { }
            return rows;
        }

        private static void Normalize(EnemyStatRow v)
        {
            if (v.MaxHp < 1) v.MaxHp = 1;
            if (v.Str < 0) v.Str = 0;
            if (v.Sp < 0) v.Sp = 0;
            if (v.Def < 0) v.Def = 0;
            if (v.Agt < 0) v.Agt = 0;
            if (v.BodySize < 0) v.BodySize = 0;
            if (float.IsNaN(v.DefRatePhysical) || float.IsInfinity(v.DefRatePhysical)) v.DefRatePhysical = 0f;
            if (float.IsNaN(v.DefRateMagic) || float.IsInfinity(v.DefRateMagic)) v.DefRateMagic = 0f;
            if (v.BodyType < 0) v.BodyType = 0;
            if (v.SkillType < 0) v.SkillType = 0;
        }

        private static void WriteRow(EnemyListData ed, EnemyStatRow v)
        {
            ed.maxhp = v.MaxHp;
            ed.str = v.Str;
            ed.sp = v.Sp;
            ed.def = v.Def;
            ed.agt = v.Agt;
            ed.bodysize = v.BodySize;
            ed.defratephysical = v.DefRatePhysical;
            ed.defratemagic = v.DefRateMagic;
            ed.bodytype = (BodyType)v.BodyType;
            ed.skilltype = (SkillType)v.SkillType;
        }

        /// <summary>写入一种敌人的全部可改数值，并同步到场上实例。</summary>
        public static bool SetEnemyStats(EnemyStatRow v)
        {
            if (v == null) return false;
            var ed = FindEnemyData(v.Id);
            if (ed == null) return false;

            Normalize(v);
            if (!_enemyOrigStat.ContainsKey(v.Id)) _enemyOrigStat[v.Id] = ReadRow(ed);

            try { WriteRow(ed, v); }
            catch (Exception e)
            {
                Plugin.LogSource?.LogWarning("[敌人] 写入失败 " + v.Id + ": " + e.Message);
                return false;
            }

            _enemyOverride[v.Id] = v.Clone();
            int live = ApplyEnemyOverridesToBattle();
            Plugin.LogSource?.LogInfo("[敌人] " + v.Id + " 生命=" + v.MaxHp + " 力量=" + v.Str + " 魔力=" + v.Sp
                + " 防御=" + v.Def + " 防御属性=" + BodyTypeName(v.BodyType) + " 物抗=" + v.DefRatePhysical
                + " 魔抗=" + v.DefRateMagic + " 敏捷=" + v.Agt + " 技能属性=" + SkillTypeName(v.SkillType)
                + " 大小=" + v.BodySize + "（同步场上 " + live + " 个）");
            return true;
        }

        /// <summary>按倍率缩放全部敌人的生命 / 力量 / 魔力 / 防御 / 敏捷（以原始值为基准）。</summary>
        public static int ScaleAllEnemyStats(float multiplier)
        {
            if (multiplier < 0.01f) multiplier = 0.01f;
            var ds = DS;
            if (ds == null) return 0;
            int changed = 0;
            try
            {
                var el = ds.enemyList;
                if (IsNull(el)) return 0;
                var arr = el.dataArray;
                if (IsNull(arr)) return 0;
                for (int i = 0; i < arr.Length; i++)
                {
                    var ed = arr[i];
                    if (IsNull(ed) || string.IsNullOrEmpty(ed.id)) continue;
                    if (!_enemyOrigStat.ContainsKey(ed.id)) _enemyOrigStat[ed.id] = ReadRow(ed);

                    var origin = _enemyOrigStat[ed.id];
                    var v = ReadRow(ed);
                    v.MaxHp = (int)Math.Round(origin.MaxHp * multiplier);
                    v.Str = (int)Math.Round(origin.Str * multiplier);
                    v.Sp = (int)Math.Round(origin.Sp * multiplier);
                    v.Def = (int)Math.Round(origin.Def * multiplier);
                    v.Agt = (int)Math.Round(origin.Agt * multiplier);
                    Normalize(v);
                    WriteRow(ed, v);
                    _enemyOverride[ed.id] = v.Clone();
                    changed++;
                }
            }
            catch { }
            Plugin.LogSource?.LogInfo("[敌人] 全表 x" + multiplier + "，改写 " + changed + " 种");
            if (changed > 0) ApplyEnemyOverridesToBattle();
            return changed;
        }

        /// <summary>恢复一种敌人的原始数值。</summary>
        public static bool RestoreEnemyStats(string id)
        {
            var ed = FindEnemyData(id);
            if (ed == null) return false;
            if (!_enemyOrigStat.TryGetValue(id, out EnemyStatRow origin)) return false;
            try { WriteRow(ed, origin); }
            catch { return false; }
            _enemyOrigStat.Remove(id);
            _enemyOverride.Remove(id);
            ApplyEnemyOverridesToBattle();
            Plugin.LogSource?.LogInfo("[敌人] " + id + " 已恢复原始数值");
            return true;
        }

        /// <summary>恢复全部敌人。</summary>
        public static int RestoreAllEnemyStats()
        {
            var ds = DS;
            if (ds == null) return 0;
            int changed = 0;
            try
            {
                var el = ds.enemyList;
                if (IsNull(el)) return 0;
                var arr = el.dataArray;
                if (IsNull(arr)) return 0;
                for (int i = 0; i < arr.Length; i++)
                {
                    var ed = arr[i];
                    if (IsNull(ed) || string.IsNullOrEmpty(ed.id)) continue;
                    if (!_enemyOrigStat.TryGetValue(ed.id, out EnemyStatRow origin)) continue;
                    WriteRow(ed, origin);
                    changed++;
                }
                _enemyOrigStat.Clear();
                _enemyOverride.Clear();
            }
            catch { }
            Plugin.LogSource?.LogInfo("[敌人] 全部恢复，共 " + changed + " 种");
            if (changed > 0) ApplyEnemyOverridesToBattle();
            return changed;
        }

        // ---- 场上实例（EnemyBasic.status = EnemyBasic.BaseStat） ----

        private static bool _enemyScanWarned;

        private static List<EnemyBasic> AllLiveEnemies()
        {
            var list = new List<EnemyBasic>();
            try
            {
                var arr = UnityEngine.Object.FindObjectsOfType(Il2CppInterop.Runtime.Il2CppType.Of<EnemyBasic>());
                if (arr != null)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        EnemyBasic eb = null;
                        try { eb = arr[i]?.TryCast<EnemyBasic>(); } catch { }
                        if (eb != null && eb.Pointer != IntPtr.Zero) list.Add(eb);
                    }
                }
            }
            catch (Exception e)
            {
                if (!_enemyScanWarned)
                {
                    _enemyScanWarned = true;
                    Plugin.LogSource?.LogWarning("[敌人] 扫描场上敌人失败: " + e.GetType().Name + " " + e.Message);
                }
            }
            return list;
        }

        public static int CountLiveEnemies(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            int n = 0;
            foreach (var eb in AllLiveEnemies())
            {
                try { if (eb.enemyID == id) n++; } catch { }
            }
            return n;
        }

        private static bool ApplyToInstance(EnemyBasic eb, EnemyStatRow t)
        {
            EnemyBasic.BaseStat st = null;
            try { st = eb.status; } catch { }
            if (st == null || st.Pointer == IntPtr.Zero) return false;

            int oldMax, oldHp;
            try { oldMax = st.maxHP; oldHp = st.hp; } catch { return false; }
            if (oldMax <= 0) return false;   // 还没初始化，等下一轮

            try
            {
                st.maxHP = t.MaxHp;
                st.power = t.Str;
                st.sp = t.Sp;
                st.def = t.Def;
                st.agt = t.Agt;
                st.bodySize = t.BodySize;
                st.defRate_physical = t.DefRatePhysical;
                st.defRate_magic = t.DefRateMagic;
                st.bodyType = (BodyType)t.BodyType;
                st.skillType = (SkillType)t.SkillType;

                int newHp;
                if (oldHp <= 0) newHp = 0;
                else if (oldHp >= oldMax) newHp = t.MaxHp;
                else newHp = (int)Math.Round((double)oldHp * t.MaxHp / oldMax);
                if (oldHp > 0 && newHp < 1) newHp = 1;
                if (newHp > t.MaxHp) newHp = t.MaxHp;
                st.hp = newHp;
            }
            catch { return false; }
            return true;
        }

        /// <summary>把所有覆盖值无条件套用到场上实例（用户点按钮 / 刚改完时调用）。</summary>
        public static int ApplyEnemyOverridesToBattle()
        {
            if (_enemyOverride.Count == 0) return 0;
            int n = 0;
            foreach (var eb in AllLiveEnemies())
            {
                try
                {
                    if (string.IsNullOrEmpty(eb.enemyID)) continue;
                    if (!_enemyOverride.TryGetValue(eb.enemyID, out EnemyStatRow t)) continue;
                    if (ApplyToInstance(eb, t)) n++;
                }
                catch { }
            }
            return n;
        }

        /// <summary>
        /// 周期检查：只对数值还没对上的实例动手。
        /// 新刷出来的怪会被自动套用，而战斗中的正常掉血不会被反复覆盖。
        /// </summary>
        public static int EnforceEnemyOverrides()
        {
            if (_enemyOverride.Count == 0) return 0;
            int n = 0;
            foreach (var eb in AllLiveEnemies())
            {
                try
                {
                    if (string.IsNullOrEmpty(eb.enemyID)) continue;
                    if (!_enemyOverride.TryGetValue(eb.enemyID, out EnemyStatRow t)) continue;
                    var st = eb.status;
                    if (st == null || st.Pointer == IntPtr.Zero) continue;
                    if (st.maxHP == t.MaxHp && st.power == t.Str && st.sp == t.Sp && st.def == t.Def
                        && st.agt == t.Agt && st.bodySize == t.BodySize
                        && Math.Abs(st.defRate_physical - t.DefRatePhysical) < 0.0001f
                        && Math.Abs(st.defRate_magic - t.DefRateMagic) < 0.0001f
                        && (int)st.bodyType == t.BodyType && (int)st.skillType == t.SkillType) continue;
                    if (ApplyToInstance(eb, t)) n++;
                }
                catch { }
            }
            if (n > 0) Plugin.LogSource?.LogInfo("[敌人] 自动套用到 " + n + " 个新出现的实例");
            return n;
        }

    }
}
