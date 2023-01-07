using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;
using I2.Loc;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;


namespace NovaAchievments
{
    public class Main : Mod
    {
        static RectTransform prefabParent;
        static GameObject uiPrefab;
        public static GameObject entryPrefab;
        public static MenuType ownUI = (MenuType)53;
        public static Dictionary<string,Ach> Achievements = new Dictionary<string, Ach>();
        public void Start()
        {
            prefabParent = new GameObject("prefabParent", typeof(RectTransform)).GetComponent<RectTransform>();
            DontDestroyOnLoad(prefabParent);
            prefabParent.gameObject.SetActive(false);

            var OptionMenuParent = Traverse.Create(ComponentManager<Settings>.Value).Field("optionsCanvas").GetValue<GameObject>().transform.Find("OptionMenuParent");
            var background = OptionMenuParent.Find("BrownBackground");
            var closeButton = OptionMenuParent.Find("CloseButton").GetComponent<Button>();
            var contentBox = OptionMenuParent.Find("TabContent/Graphics");

            uiPrefab = new GameObject("AchievementUI", typeof(RectTransform), typeof(GraphicRaycaster));
            uiPrefab.GetComponent<GraphicRaycaster>().enabled = false;
            var uiRect = uiPrefab.GetComponent<RectTransform>();
            uiRect.SetParent(prefabParent, false);
            uiRect.anchorMin = Vector2.one * 0.5f;
            uiRect.anchorMax = Vector2.one * 0.5f;
            var optionsSize = OptionMenuParent.GetComponent<RectTransform>().sizeDelta;
            uiRect.offsetMin = -optionsSize / 2;
            uiRect.offsetMax = optionsSize / 2;
            var newBackground = Instantiate(background, uiRect, false).GetComponent<RectTransform>();
            newBackground.name = background.name;
            newBackground.anchorMin = Vector2.zero;
            newBackground.anchorMax = Vector2.one;
            newBackground.offsetMin = Vector2.zero;
            newBackground.offsetMax = Vector2.zero;
            var newClose = Instantiate(closeButton, uiRect, false);
            newClose.name = closeButton.name;
            newClose.onClick = new Button.ButtonClickedEvent();
            var newCloseRect = newClose.GetComponent<RectTransform>();
            var closeSize = newCloseRect.sizeDelta;
            newCloseRect.anchorMin = Vector2.one;
            newCloseRect.anchorMax = Vector2.one;
            newCloseRect.offsetMin = -closeSize * 1.5f;
            newCloseRect.offsetMax = -closeSize / 2;
            var newContent = Instantiate(contentBox, uiRect, false).GetComponent<RectTransform>();
            newContent.name = "Container";
            newContent.gameObject.SetActive(true);
            newContent.anchorMin = Vector2.zero;
            newContent.anchorMax = Vector2.one;
            newContent.offsetMin = closeSize / 2;
            newContent.offsetMax = closeSize * new Vector2(-0.5f, -2f);
            DestroyImmediate(newContent.GetComponent<GraphicsSettingsBox>());
            var fitter = newContent.Find("Viewport/Content").gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.MinSize;
            var scroll = newContent.Find("Scrollbar Vertical").GetComponent<Scrollbar>();
            newContent.GetComponent<ScrollRect>().verticalScrollbar = scroll;
            scroll.value = 1;
            foreach (Transform t in newContent.Find("Viewport/Content"))
                Destroy(t.gameObject);
            

            entryPrefab = Instantiate(OptionMenuParent.Find("TabContent/Controls/Viewport/Content/ResetKeybinds").gameObject, prefabParent, false);
            entryPrefab.name = "Entry";
            var entryLabel = Instantiate(OptionMenuParent.Find("TabContent/General/Viewport/Content/Language/Label"),entryPrefab.transform);
            entryLabel.name = "Label";
            var labelRect = entryLabel.GetComponent<RectTransform>();
            labelRect.offsetMax = -labelRect.offsetMin;
            entryLabel.GetComponent<Text>().text = "Entry Name";
            Button button = entryPrefab.transform.Find("ResetKeybindsButton").GetComponent<Button>();
            button.name = "Button";
            button.onClick = new Button.ButtonClickedEvent();
            button.transform.Find("Text").GetComponent<Text>().text = "Button Name";
            destroyLocalizations(entryPrefab);

            if (ComponentManager<CanvasHelper>.Value) Patch_CanvasHelper_Create.Prefix(ComponentManager<CanvasHelper>.Value);

            Log("Mod has been loaded!");
        }

        public void OnModUnload()
        {
            Log("Mod has been unloaded!");
        }

        void Update()
        {
            if (ComponentManager<CanvasHelper>.Value && CanvasHelper.ActiveMenu == MenuType.None && Input.GetKeyDown(KeyCode.Y))
                    ComponentManager<CanvasHelper>.Value.OpenMenu(ownUI);
            if (CanvasHelper.ActiveMenu == ownUI && MyInput.GetButtonDown("Cancel"))
                ComponentManager<CanvasHelper>.Value.CloseMenu(ownUI);
        }

        public static void destroyLocalizations(GameObject gO)
        {
            foreach (Localize localize in gO.GetComponentsInChildren<Localize>())
            {
                localize.enabled = false;
                DestroyImmediate(localize, true);
            }
        }

        public static void LogTree(Transform transform)
        {
            Debug.Log(GetLogTree(transform));
        }

        public static string GetLogTree(Transform transform, string prefix = " -")
        {
            string str = "\n" + prefix + transform.name;
            foreach (Object obj in transform.GetComponents<Object>())
                str += ": " + obj.GetType().Name;
            foreach (Transform sub in transform)
                str += GetLogTree(sub, prefix + "--");
            return str;
        }

        public static GameObject CreateUI()
        {
            var CurrentUI = Instantiate(uiPrefab, null, false);
            CurrentUI.transform.SetParent(ComponentManager<CanvasHelper>.Value.transform, false);
            CurrentUI.GetComponent<GraphicRaycaster>().enabled = true;
            var close = CurrentUI.transform.Find("CloseButton").GetComponent<Button>();
            close.onClick.AddListener(CloseMenu);
            return CurrentUI;
        }

        static void CloseMenu()
        {
            if (ComponentManager<CanvasHelper>.Value)
                ComponentManager<CanvasHelper>.Value.CloseMenu(ownUI);
        }
    }

    public class AchievementUI : MonoBehaviour
    {
        public void MenuOpen()
        {
            var content = transform.Find("Container/Viewport/Content");
            foreach (Transform t in content)
                Destroy(t.gameObject);
            var alt = false;
            foreach (var f in Main.Achievements.Values)
            {
                alt = !alt;
                var o = Instantiate(Main.entryPrefab, content, false);
                var i = o.transform.Find("Button").GetComponent<Button>();
                i.onClick.AddListener(() =>
                {
                    Debug.Log("this is a placeholder message");
                });
                var text = i.transform.Find("Text").GetComponent<Text>();
                var button = i.GetComponent<RectTransform>();

                if (o && o.GetComponent<Image>())
                    o.GetComponent<Image>().enabled = alt;
                text.text = "More Info";
                button.offsetMax = new Vector2(text.preferredWidth + text.preferredHeight, button.offsetMax.y);
                o.transform.Find("Label").GetComponent<Text>().text = f.Name;
            }
        }
    }

    public class Ach
    {
        public string Id;
        public string Name;
        public string AfterId;
        public bool Achieved = false;
        public Condition[] Conditions;
        public Mode CompleteMode = Mode.All;

        public bool Complete
        {
            get
            {
                if (Achieved)
                    return true;
                if (CompleteMode == Mode.All || CompleteMode == Mode.AllOrdered)
                    return Achieved = Conditions.All(x => x.Complete);
                if (CompleteMode == Mode.Any)
                    return Achieved = Conditions.Any(x => x.Complete);
                return false;
            }
        }

        public bool Check(Item_Base Item, int Count = 1) => Check(x => x.Check(Item, Count));
        bool Check(Predicate<Condition> action)
        {
            if (Achieved)
                return true;
            if (CompleteMode == Mode.All)
                foreach (var c in Conditions)
                    action(c);
            else if (CompleteMode == Mode.AllOrdered)
                foreach (var c in Conditions)
                {
                    if (!action(c))
                        break;
                }
            else if (CompleteMode == Mode.Any)
                foreach (var c in Conditions)
                {
                    if (action(c))
                        break;
                }
            return Complete;
        }

        public enum Mode
        {
            All,
            AllOrdered,
            Any
        }
        public class Condition
        {
            string item;
            int amount;
            int current = 0;
            bool completed = false;
            public bool CanBecomeIncomplete;

            public Condition(string Item, int Count = 1)
            {
                item = Item;
                amount = Count;
            }
            public bool Check(Item_Base Item, int Count = 1)
            {
                if (item != null && Item && (Item.UniqueName == item) || (Item.UniqueIndex.ToString() == item))
                    current += Count;
                return Complete;
            }

            public bool Complete => (CanBecomeIncomplete || !completed) ? completed = amount <= current : completed;
        }
    }

    [HarmonyPatch(typeof(CanvasHelper), "Awake")]
    class Patch_CanvasHelper_Create
    {
        public static void Prefix(CanvasHelper __instance)
        {
            var menusT = Traverse.Create(__instance).Field<GameMenu[]>("gameMenus");
            var menus = new List<GameMenu>(menusT.Value);
            var menu = Main.CreateUI();
            menus.Add(new GameMenu()
            {
                menuType = Main.ownUI,
                canvasRaycaster = menu.GetComponent<GraphicRaycaster>(),
                menuObjects = new List<GameObject>() { menu },
                messageReciever = menu,
                recieveEventMessages = true
            });
            menusT.Value = menus.ToArray();
        }
    }
}