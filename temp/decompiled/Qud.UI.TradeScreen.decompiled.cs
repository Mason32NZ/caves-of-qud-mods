using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConsoleLib.Console;
using FuzzySharp;
using FuzzySharp.Extractor;
using UnityEngine;
using XRL;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;

namespace Qud.UI;

[UIView("ModernTrade", false, false, false, null, null, false, 0, false, NavCategory = "Trade", UICanvas = "Trade", UICanvasHost = 1)]
public class TradeScreen : SingletonWindowBase<TradeScreen>, ControlManager.IControllerChangedEvent
{
	public class Context : NavigationContext
	{
	}

	public enum SortMode
	{
		AZ,
		Category
	}

	protected TaskCompletionSource<TradeUI.OfferStatus> menucomplete = new TaskCompletionSource<TradeUI.OfferStatus>();

	public ScrollContext<NavigationContext> vertNav = new ScrollContext<NavigationContext>();

	public ScrollContext<NavigationContext> horizNav = new ScrollContext<NavigationContext>();

	public Context navigationContext = new Context
	{
		buttonHandlers = null
	};

	public TradeUI.TradeScreenMode mode;

	public FilterBar filterBar;

	public FrameworkScroller hotkeyBar;

	public UITextSkin hotkeyText;

	public int[] Weight = new int[2];

	public double[] Totals = new double[2];

	public List<TradeEntry>[] tradeEntries;

	public List<TradeLineData>[] listItems;

	public FrameworkScroller[] scrollerControllers;

	public List<string>[] usedCategories;

	public Dictionary<string, List<TradeEntry>>[] objectCategories;

	public Dictionary<string, bool>[] categoryCollapsed;

	public static int[][] NumberSelected = new int[2][];

	public Dictionary<XRL.World.GameObject, int> ObjectSide = new Dictionary<XRL.World.GameObject, int>();

	public Dictionary<XRL.World.GameObject, int> ObjectIndex = new Dictionary<XRL.World.GameObject, int>();

	public FrameworkSearchInput searchInput;

	public UITextSkin detailsLeftText;

	public UITextSkin detailsRightText;

	public HashSet<XRL.World.GameObject> importantWarnedObjects = new HashSet<XRL.World.GameObject>();

	private string searchText = "";

	public UnityEngine.GameObject dragIndicator;

	public UITextSkin dragIndicatorText;

	public UITextSkin[] titleText;

	public static XRL.World.GameObject Trader;

	public static float CostMultiple;

	public UIThreeColorProperties[] traderIcons;

	public UITextSkin[] traderNames;

	private TradeEntry searcher = new TradeEntry("");

	private List<TradeEntry> filteredEntries = new List<TradeEntry>();

	public MonoBehaviour[] componentsToEnableOnResize;

	public int resizeFrame;

	public int layoutGroupFrame;

	public double lastStageScale;

	public int lastWidth = int.MinValue;

	public RectTransform flowCheck;

	public SortMode sortMode = SortMode.Category;

	public static readonly MenuOption SET_FILTER = new MenuOption
	{
		Id = "SET_FILTER",
		KeyDescription = "Filter",
		InputCommand = "CmdFilter",
		Description = "Filter"
	};

	public static readonly MenuOption TOGGLE_SORT = new MenuOption
	{
		Id = "TOGGLE_SORT",
		KeyDescription = "Toggle Sort",
		InputCommand = "Toggle",
		Description = "toggle sort"
	};

	public static readonly MenuOption OFFER_TRADE = new MenuOption
	{
		Id = "OFFER_TRADE",
		KeyDescription = "Offer",
		InputCommand = "CmdTradeOffer",
		Description = "offer"
	};

	public static readonly MenuOption ADD_ONE = new MenuOption
	{
		Id = "ADD_ONE",
		KeyDescription = "Add One",
		InputCommand = "CmdTradeAdd",
		Description = "add one"
	};

	public static readonly MenuOption REMOVE_ONE = new MenuOption
	{
		Id = "REMOVE_ONE",
		KeyDescription = "Remove One",
		InputCommand = "CmdTradeRemove",
		Description = "remove one"
	};

	public static readonly MenuOption TOGGLE_ALL = new MenuOption
	{
		Id = "TOGGLE_ALL",
		KeyDescription = "Toggle All",
		InputCommand = "CmdTradeToggleAll",
		Description = "toggle all"
	};

	public static readonly MenuOption VENDOR_ACTIONS = new MenuOption
	{
		Id = "VENDOR_ACTIONS",
		KeyDescription = "Vendor Actions",
		InputCommand = "CmdVendorActions",
		Description = "vendor actions"
	};

	public static List<MenuOption> defaultMenuOptions = new List<MenuOption>
	{
		new MenuOption
		{
			Id = "Cancel",
			InputCommand = "Cancel",
			Description = "Close Menu"
		},
		new MenuOption
		{
			InputCommand = "NavigationXYAxis",
			Description = "navigate",
			disabled = true
		},
		TOGGLE_SORT,
		SET_FILTER
	};

	public static List<MenuOption> getItemMenuOptions = new List<MenuOption>
	{
		new MenuOption
		{
			Id = "Cancel",
			InputCommand = "Cancel",
			Description = "Close Menu"
		},
		new MenuOption
		{
			InputCommand = "NavigationXYAxis",
			Description = "navigate",
			disabled = true
		},
		TOGGLE_SORT,
		SET_FILTER,
		VENDOR_ACTIONS,
		ADD_ONE,
		REMOVE_ONE,
		TOGGLE_ALL
	};

	public UITextSkin[] freeDramsLabels;

	public UITextSkin[] totalLabels;

	private NavigationContext lastContext;

	public static string TradeScreenVerb
	{
		get
		{
			if (TradeUI.ScreenMode != TradeUI.TradeScreenMode.Trade || !(TradeUI.costMultiple > 0f))
			{
				return "transfer";
			}
			return "trade";
		}
	}

	public static string sortModeDescription => "sort: " + Markup.Color((SingletonWindowBase<TradeScreen>.instance.sortMode == SortMode.AZ) ? "w" : "y", "a-z") + "/" + Markup.Color((SingletonWindowBase<TradeScreen>.instance.sortMode == SortMode.Category) ? "w" : "y", "by class");

	public int selectedSide
	{
		get
		{
			if (scrollerControllers[0].scrollContext.IsActive())
			{
				return 0;
			}
			if (scrollerControllers[1].scrollContext.IsActive())
			{
				return 1;
			}
			return -1;
		}
	}

	public async void HandleTradeSome(TradeLine line)
	{
		int? num = await Popup.AskNumberAsync("Add how many " + line.context.data.go.DisplayName + " to trade.", howManySelected(line.context.data.go), 0, line.context.data.go.Count);
		if (num.HasValue)
		{
			TradeLineData data = line.context.data;
			data.numberSelected = await setHowManySelected(line.context.data.go, num.Value);
			line.setData(line.context.data);
			UpdateTotals();
		}
	}

	public int howManySelected(XRL.World.GameObject go)
	{
		return NumberSelected[ObjectSide[go]][ObjectIndex[go]];
	}

	public bool IsGoingToWarn(XRL.World.GameObject go)
	{
		if (importantWarnedObjects.Contains(go))
		{
			return false;
		}
		if (go.ShouldConfirmUseImportant(The.Player))
		{
			return true;
		}
		return false;
	}

	public async Task<int> setHowManySelected(XRL.World.GameObject go, int total)
	{
		if (TradeUI.ScreenMode == TradeUI.TradeScreenMode.Trade && !importantWarnedObjects.Contains(go) && go.ShouldConfirmUseImportant(The.Player) && ObjectSide[go] == 1)
		{
			if (!(await go.ConfirmUseImportantAsync(null, TradeScreenVerb)))
			{
				return howManySelected(go);
			}
			importantWarnedObjects.Add(go);
		}
		NumberSelected[ObjectSide[go]][ObjectIndex[go]] = total;
		if (NumberSelected[ObjectSide[go]][ObjectIndex[go]] > go.Count)
		{
			NumberSelected[ObjectSide[go]][ObjectIndex[go]] = go.Count;
		}
		if (NumberSelected[ObjectSide[go]][ObjectIndex[go]] <= 0)
		{
			NumberSelected[ObjectSide[go]][ObjectIndex[go]] = 0;
		}
		return howManySelected(go);
	}

	public async Task<int> incrementHowManySelected(XRL.World.GameObject go, int delta)
	{
		await setHowManySelected(go, howManySelected(go) + delta);
		return howManySelected(go);
	}

	public bool isCollapsed(int side, string category)
	{
		if (!categoryCollapsed[side].TryGetValue(category, out var value))
		{
			return false;
		}
		return value;
	}

	public void OnSearchTextChange(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			searchText = "";
		}
		else
		{
			searchText = text;
		}
		BeforeShow();
	}

	public void SetupContext()
	{
		filterBar.SetupContext(includeScrollContext: false);
		filterBar.OnSearchTextChange.RemoveAllListeners();
		filterBar.OnSearchTextChange.AddListener(OnSearchTextChange);
		filterBar.filtersUpdated = BeforeShow;
		filterBar.selectedPosition = 0;
		vertNav.parentContext = navigationContext;
		vertNav.SetAxis(InputAxisTypes.NavigationYAxis);
		vertNav.contexts.Clear();
		vertNav.contexts.Add(filterBar.scrollContext);
		vertNav.contexts.Add(horizNav);
		vertNav.contexts.Add(filterBar.rootContext);
		horizNav.parentContext = vertNav;
		horizNav.contexts.Clear();
		horizNav.contexts.Add(scrollerControllers[0].scrollContext);
		horizNav.contexts.Add(scrollerControllers[1].scrollContext);
		horizNav.SetAxis(InputAxisTypes.NavigationXAxis);
		Context context = navigationContext;
		if (context.buttonHandlers == null)
		{
			context.buttonHandlers = new Dictionary<InputButtonTypes, Action> { 
			{
				InputButtonTypes.CancelButton,
				XRL.UI.Framework.Event.Helpers.Handle(Cancel)
			} };
		}
		context = navigationContext;
		if (context.commandHandlers == null)
		{
			context.commandHandlers = new Dictionary<string, Action>
			{
				{
					SET_FILTER.InputCommand,
					XRL.UI.Framework.Event.Helpers.Handle(delegate
					{
						HandleMenuOption(SET_FILTER);
					})
				},
				{
					TOGGLE_SORT.InputCommand,
					XRL.UI.Framework.Event.Helpers.Handle(delegate
					{
						HandleMenuOption(TOGGLE_SORT);
					})
				},
				{
					OFFER_TRADE.InputCommand,
					XRL.UI.Framework.Event.Helpers.Handle(delegate
					{
						HandleMenuOption(OFFER_TRADE);
					})
				},
				{
					ADD_ONE.InputCommand,
					XRL.UI.Framework.Event.Helpers.Handle(delegate
					{
						HandleMenuOption(ADD_ONE);
					})
				},
				{
					REMOVE_ONE.InputCommand,
					XRL.UI.Framework.Event.Helpers.Handle(delegate
					{
						HandleMenuOption(REMOVE_ONE);
					})
				},
				{
					TOGGLE_ALL.InputCommand,
					XRL.UI.Framework.Event.Helpers.Handle(delegate
					{
						HandleMenuOption(TOGGLE_ALL);
					})
				},
				{
					"Category Left",
					XRL.UI.Framework.Event.Helpers.Handle(delegate
					{
						filterBar.CategoryLeft();
					})
				},
				{
					"Category Right",
					XRL.UI.Framework.Event.Helpers.Handle(delegate
					{
						filterBar.CategoryRight();
					})
				}
			};
		}
		TOGGLE_SORT.Description = sortModeDescription;
		vertNav.Setup();
		horizNav.wraps = true;
		horizNav.Setup();
		vertNav.wraps = true;
		vertNav.Setup();
		scrollerControllers[0].scrollContext.parentContext = horizNav;
		scrollerControllers[1].scrollContext.parentContext = horizNav;
	}

	public void BackgroundClicked()
	{
		if (navigationContext.IsActive())
		{
			Cancel();
		}
	}

	protected async Task<TradeUI.OfferStatus> showScreen(XRL.World.GameObject trader, float costMultiple = 1f, TradeUI.TradeScreenMode screenMode = TradeUI.TradeScreenMode.Trade)
	{
		The.Player.IsRealityDistortionUsable();
		mode = screenMode;
		Trader = trader;
		CostMultiple = costMultiple;
		searchText = "";
		menucomplete.TrySetCanceled();
		menucomplete = new TaskCompletionSource<TradeUI.OfferStatus>();
		if (tradeEntries == null)
		{
			tradeEntries = new List<TradeEntry>[2];
			tradeEntries[0] = new List<TradeEntry>();
			tradeEntries[1] = new List<TradeEntry>();
			listItems = new List<TradeLineData>[2];
			listItems[0] = new List<TradeLineData>();
			listItems[1] = new List<TradeLineData>();
			usedCategories = new List<string>[2];
			usedCategories[0] = new List<string>();
			usedCategories[1] = new List<string>();
			objectCategories = new Dictionary<string, List<TradeEntry>>[2];
			objectCategories[0] = new Dictionary<string, List<TradeEntry>>();
			objectCategories[1] = new Dictionary<string, List<TradeEntry>>();
			categoryCollapsed = new Dictionary<string, bool>[2];
			categoryCollapsed[0] = new Dictionary<string, bool>();
			categoryCollapsed[1] = new Dictionary<string, bool>();
		}
		ClearAndSetupTradeUI();
		MinEvent.UIHold = true;
		GameManager.Instance.PushGameView("ModernTrade");
		try
		{
			await The.UiContext;
			importantWarnedObjects.Clear();
			filterBar.ResetFilters();
			BeforeShow();
			Show();
			Canvas.ForceUpdateCanvases();
			if (tradeEntries[0].Count > 0)
			{
				scrollerControllers[0].scrollContext.ActivateAndEnable();
			}
			else if (tradeEntries[1].Count > 0)
			{
				scrollerControllers[1].scrollContext.ActivateAndEnable();
			}
			else
			{
				filterBar.scrollContext.ActivateAndEnable();
			}
			TradeUI.OfferStatus result = await menucomplete.Task;
			Cleanup();
			return result;
		}
		finally
		{
			GameManager.Instance.PopGameView(bHard: true);
			MinEvent.UIHold = false;
		}
	}

	public void ClearAndSetupTradeUI()
	{
		tradeEntries[0].Clear();
		tradeEntries[1].Clear();
		listItems[0].Clear();
		listItems[1].Clear();
		TradeUI.GetObjects(Trader, tradeEntries[0], The.Player, TradeUI.costMultiple);
		TradeUI.GetObjects(The.Player, tradeEntries[1], Trader, TradeUI.costMultiple);
		NumberSelected[0] = new int[tradeEntries[0].Count];
		NumberSelected[1] = new int[tradeEntries[1].Count];
		ObjectSide.Clear();
		ObjectIndex.Clear();
		for (int i = 0; i <= 1; i++)
		{
			for (int j = 0; j < tradeEntries[i].Count; j++)
			{
				if (tradeEntries[i][j].GO != null)
				{
					if (ObjectSide.ContainsKey(tradeEntries[i][j].GO) || ObjectIndex.ContainsKey(tradeEntries[i][j].GO))
					{
						MetricsManager.LogError("Trade object dupe " + tradeEntries[i][j].GO.Blueprint);
						tradeEntries[i].RemoveAt(j);
						j--;
					}
					else
					{
						ObjectSide.Add(tradeEntries[i][j].GO, i);
						ObjectIndex.Add(tradeEntries[i][j].GO, j);
					}
				}
			}
		}
	}

	public static Task<TradeUI.OfferStatus> show(XRL.World.GameObject trader, float costMultiple, TradeUI.TradeScreenMode screenMode)
	{
		return NavigationController.instance.SuspendContextWhile(() => SingletonWindowBase<TradeScreen>.instance.showScreen(trader, costMultiple, screenMode));
	}

	public virtual void ResetLayoutFrame()
	{
		if (componentsToEnableOnResize != null)
		{
			MonoBehaviour[] array = componentsToEnableOnResize;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].enabled = true;
			}
		}
		lastStageScale = Options.StageScale;
		lastWidth = Screen.width;
		layoutGroupFrame = 2;
	}

	public void UpdateViewFromData()
	{
		XRL.World.Event.PinCurrentPool();
		ControlManager.GetHotkeySpread(new List<string> { "Menus", "UINav", "Trade", "UI" });
		List<TradeLineData>[] array = listItems;
		foreach (List<TradeLineData> obj in array)
		{
			obj.ForEach(delegate(TradeLineData tradeLineData3)
			{
				tradeLineData3.free();
			});
			obj.Clear();
		}
		List<string> list = new List<string>();
		list.Add("*All");
		List<TradeEntry>[] array2 = tradeEntries;
		for (int i = 0; i < array2.Length; i++)
		{
			foreach (TradeEntry item in array2[i])
			{
				if (item.GO != null)
				{
					string inventoryCategory = item.GO.GetInventoryCategory();
					if (!list.Contains(inventoryCategory))
					{
						list.Add(inventoryCategory);
					}
				}
			}
		}
		for (int num = 0; num < 2; num++)
		{
			FrameworkScroller frameworkScroller = scrollerControllers[num];
			List<TradeEntry> list2 = tradeEntries[num];
			List<string> list3 = usedCategories[num];
			Dictionary<string, List<TradeEntry>> dictionary = objectCategories[num];
			List<TradeLineData> list4 = listItems[num];
			filteredEntries.Clear();
			if (searchText.IsNullOrEmpty() && filterBar.enabledCategories.Count == 1 && filterBar.enabledCategories[0] == "*All")
			{
				filteredEntries.AddRange(list2);
			}
			else if (searchText.IsNullOrEmpty())
			{
				filteredEntries.AddRange(list2.Where((TradeEntry tradeEntry) => filterBar.enabledCategories.Contains("*All") || filterBar.enabledCategories.Contains(tradeEntry.CategoryName) || (tradeEntry.GO != null && filterBar.enabledCategories.Contains(tradeEntry.GO.GetInventoryCategory()))));
			}
			else
			{
				searcher.CategoryName = searchText;
				filteredEntries.AddRange(from m in Process.ExtractTop(searcher, list2, (TradeEntry tradeEntry) => tradeEntry.NameForSort.ToLower(), null, list2.Count, 50)
					select m.Value into tradeEntry
					where filterBar.enabledCategories.Contains("*All") || filterBar.enabledCategories.Contains(tradeEntry.CategoryName) || (tradeEntry.GO != null && filterBar.enabledCategories.Contains(tradeEntry.GO.GetInventoryCategory()))
					select tradeEntry);
			}
			if (sortMode == SortMode.Category)
			{
				list3.Clear();
				foreach (KeyValuePair<string, List<TradeEntry>> item2 in dictionary)
				{
					item2.Value.Clear();
				}
				foreach (TradeEntry filteredEntry in filteredEntries)
				{
					if (filteredEntry.GO != null)
					{
						string inventoryCategory2 = filteredEntry.GO.GetInventoryCategory();
						if (!dictionary.ContainsKey(inventoryCategory2))
						{
							dictionary.Add(inventoryCategory2, new List<TradeEntry>());
						}
						dictionary[inventoryCategory2].Add(filteredEntry);
						if (!list3.Contains(inventoryCategory2))
						{
							list3.Add(inventoryCategory2);
						}
					}
				}
				list3.Sort();
			}
			if (sortMode == SortMode.Category)
			{
				foreach (string item3 in list3)
				{
					list4.Add(PooledFrameworkDataElement<TradeLineData>.next().set(num, TradeLineDataType.Category, TradeLineDataStyle.Interact, null, null, traderInventory: false, 0, item3, isCollapsed(num, item3)));
					if (isCollapsed(num, item3))
					{
						continue;
					}
					dictionary[item3].Sort((TradeEntry a, TradeEntry b) => a.CategoryName.CompareTo(b.CategoryName));
					foreach (TradeEntry item4 in dictionary[item3])
					{
						TradeLineData tradeLineData = PooledFrameworkDataElement<TradeLineData>.next();
						int side = num;
						XRL.World.GameObject gO = item4.GO;
						int i = howManySelected(item4.GO);
						list4.Add(tradeLineData.set(side, TradeLineDataType.Item, TradeLineDataStyle.Interact, gO, item4, num == 0, i, item3, collapsed: false, indent: true));
					}
				}
			}
			else if (sortMode == SortMode.AZ)
			{
				filteredEntries.Sort((TradeEntry a, TradeEntry b) => a.NameForSort.CompareTo(b.NameForSort));
				foreach (TradeEntry item5 in filteredEntries.Where((TradeEntry tradeEntry) => tradeEntry.GO != null))
				{
					TradeLineData tradeLineData2 = PooledFrameworkDataElement<TradeLineData>.next();
					XRL.World.GameObject gO2 = item5.GO;
					int i = howManySelected(item5.GO);
					list4.Add(tradeLineData2.set(0, TradeLineDataType.Item, TradeLineDataStyle.Interact, gO2, item5, num == 0, i, null, collapsed: false, indent: true));
				}
			}
			frameworkScroller.BeforeShow(list4);
			frameworkScroller.onSelected.RemoveAllListeners();
			frameworkScroller.onSelected.AddListener(HandleSelectItem);
			frameworkScroller.onHighlight.RemoveAllListeners();
			frameworkScroller.onHighlight.AddListener(HandleHighlightObject);
			frameworkScroller.scrollContext.wraps = false;
			foreach (FrameworkUnityScrollChild selectionClone in frameworkScroller.selectionClones)
			{
				selectionClone.GetComponent<TradeLine>().screen = this;
			}
		}
		filterBar.SetCategories(list);
		UpdateTotals();
		UpdateMenuBars();
		UpdateTitleBars();
		XRL.World.Event.ResetToPin();
	}

	public void BeforeShow()
	{
		ResetLayoutFrame();
		SetupContext();
		Rect rect = flowCheck.rect;
		UpdateViewFromData();
		if (rect.width != flowCheck.rect.width || rect.height != flowCheck.rect.height)
		{
			Canvas.ForceUpdateCanvases();
			UpdateViewFromData();
		}
	}

	public void UpdateTitleBars()
	{
		if (Trader != null)
		{
			traderIcons[0].FromRenderable(Trader.RenderForUI("Trade,Title"));
		}
		traderIcons[1].FromRenderable(The.Player.RenderForUI("Trade,Title"));
		if (The.Player.Render.getHFlip())
		{
			traderIcons[1].transform.localScale = new Vector3(-1f, 1f, 1f);
		}
		else
		{
			traderIcons[1].transform.localScale = new Vector3(1f, 1f, 1f);
		}
		if (Trader != null)
		{
			traderNames[0].SetText(Trader.BaseDisplayName);
		}
		traderNames[1].SetText(The.Player.DisplayName);
	}

	public IEnumerable<FrameworkDataElement> yieldMenuOptions()
	{
		foreach (MenuOption getItemMenuOption in getItemMenuOptions)
		{
			yield return getItemMenuOption;
		}
		if (!(NavigationController.instance.activeContext is TradeLine.Context))
		{
			yield break;
		}
		foreach (MenuOption menuOptionDescription in NavigationController.instance.activeContext.menuOptionDescriptions)
		{
			yield return menuOptionDescription;
		}
	}

	public void UpdateTotals()
	{
		TradeUI.UpdateTotals(Totals, Weight, tradeEntries, NumberSelected);
		int carriedWeight = The.Player.GetCarriedWeight();
		int maxCarriedWeight = The.Player.GetMaxCarriedWeight();
		int num = (int)(LiquidVolume.GetLiquid("water").Weight * (double)TradeUI.CalculateTrade(Totals[0], Totals[1]));
		int num2 = Math.Max(0, carriedWeight + Weight[0] - Weight[1] - num);
		string text = "K";
		if (num2 > maxCarriedWeight)
		{
			text = "R";
		}
		totalLabels[0].SetText("{{B|" + TradeUI.FormatPrice(Totals[0], CostMultiple) + " drams →}}");
		totalLabels[1].SetText("{{B|← " + TradeUI.FormatPrice(Totals[1], CostMultiple) + " drams}}");
		freeDramsLabels[0].SetText($"{{{{W|${Trader?.GetFreeDrams() ?? 0}}}}}");
		freeDramsLabels[1].SetText($"{{{{W|${The.Player?.GetFreeDrams() ?? 0}}}}} | {{{{{text}|{num2}/{maxCarriedWeight} lbs.}}}}");
	}

	public void UpdateMenuBars()
	{
		hotkeyText.SetText("{{W|[" + ControlManager.getCommandInputFormatted("CmdTradeOffer") + "]}}");
		hotkeyBar.BeforeShow(null, yieldMenuOptions());
		hotkeyBar.GetNavigationContext().disabled = true;
		hotkeyBar.onSelected.RemoveAllListeners();
		hotkeyBar.onSelected.AddListener(HandleMenuOption);
	}

	public void HandleOfferTrade()
	{
		HandleMenuOption(OFFER_TRADE);
	}

	public void HandleVPositive()
	{
		categoryCollapsed[0].Clear();
		categoryCollapsed[1].Clear();
		UpdateViewFromData();
	}

	public void HandleVNegative()
	{
		categoryCollapsed[0].Clear();
		categoryCollapsed[1].Clear();
		foreach (string item in usedCategories[0])
		{
			categoryCollapsed[0][item] = true;
		}
		foreach (string item2 in usedCategories[1])
		{
			categoryCollapsed[1][item2] = true;
		}
		UpdateViewFromData();
	}

	public async void HandleMenuOption(FrameworkDataElement element)
	{
		if (element.Id == "Cancel")
		{
			Cancel();
		}
		if (element == SET_FILTER)
		{
			searchInput.EnterAndOpen();
		}
		if (element == TOGGLE_SORT)
		{
			if (sortMode == SortMode.AZ)
			{
				sortMode = SortMode.Category;
			}
			else if (sortMode == SortMode.Category)
			{
				sortMode = SortMode.AZ;
			}
			BeforeShow();
		}
		if (element == TOGGLE_ALL)
		{
			int side = selectedSide;
			if (side > -1)
			{
				bool flag = false;
				for (int i = 0; i < listItems[side].Count; i++)
				{
					if (listItems[side][i].go != null && howManySelected(listItems[side][i].go) < listItems[side][i].go.Count && !listItems[side][i].go.IsImportant())
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					foreach (TradeLineData item in listItems[side])
					{
						if (item.go != null && (side != 1 || item.go == null || !item.go.IsImportant()))
						{
							await setHowManySelected(item.go, item.go.Count);
						}
					}
				}
				else
				{
					foreach (TradeLineData item2 in listItems[side])
					{
						if (item2.go != null)
						{
							await setHowManySelected(item2.go, 0);
						}
					}
				}
				UpdateViewFromData();
			}
		}
		if (element == OFFER_TRADE)
		{
			UpdateTotals();
			int Difference = TradeUI.CalculateTrade(Totals[0], Totals[1]);
			TutorialManager.OnTradeOffer();
			TradeUI.OfferStatus offerStatus = await APIDispatch.RunAndWaitAsync(() => TradeUI.PerformOffer(Difference, forceComplete: false, Trader, mode, tradeEntries, NumberSelected));
			TutorialManager.OnTradeComplete();
			switch (offerStatus)
			{
			case TradeUI.OfferStatus.NEXT:
				return;
			case TradeUI.OfferStatus.REFRESH:
			case TradeUI.OfferStatus.TOP:
				ClearAndSetupTradeUI();
				UpdateViewFromData();
				return;
			case TradeUI.OfferStatus.CLOSE:
				Hide();
				break;
			}
			Cleanup();
			menucomplete.TrySetResult(offerStatus);
		}
	}

	public void Cleanup()
	{
	}

	public override void Hide()
	{
		base.Hide();
	}

	public void Cancel()
	{
		ControlManager.ResetInput();
		Hide();
		Cleanup();
		menucomplete.TrySetResult(TradeUI.OfferStatus.CLOSE);
	}

	void ControlManager.IControllerChangedEvent.ControllerChanged()
	{
	}

	public void HandleVAxis(int? val)
	{
	}

	public void HandleXAxis(int? val)
	{
	}

	public void ReselectAfterCategoryStateChange(TradeLineData item)
	{
		for (int i = 0; i < 2; i++)
		{
			int num = -1;
			if (item.go != null)
			{
				num = listItems[i].FindIndex((TradeLineData tradeLineData) => tradeLineData.go == item.go);
			}
			if (num == -1)
			{
				num = listItems[i].FindIndex((TradeLineData tradeLineData) => tradeLineData.category == item.category);
			}
			num = Math.Max(0, Math.Min(num, listItems[i].Count - 1));
			scrollerControllers[i].selectedPosition = num;
			scrollerControllers[i].ScrollSelectedIntoView();
			scrollerControllers[i].scrollContext.GetContextAt(num).Activate();
		}
	}

	public async void HandleItemQuickeyDown(FrameworkDataElement element)
	{
		if (!(element is TradeLineData tradeLineData))
		{
			return;
		}
		if (tradeLineData.type == TradeLineDataType.Category)
		{
			categoryCollapsed[tradeLineData.side][tradeLineData.category] = !isCollapsed(tradeLineData.side, tradeLineData.category);
			BeforeShow();
			return;
		}
		if (howManySelected(tradeLineData.go) >= tradeLineData.go.Count)
		{
			await setHowManySelected(tradeLineData.go, 0);
		}
		else
		{
			await setHowManySelected(tradeLineData.go, tradeLineData.go.Count);
		}
		BeforeShow();
	}

	public void HandleSelectItem(FrameworkDataElement element)
	{
		if (element is TradeLineData { type: TradeLineDataType.Category } tradeLineData)
		{
			categoryCollapsed[tradeLineData.side][tradeLineData.category] = !isCollapsed(tradeLineData.side, tradeLineData.category);
			BeforeShow();
		}
	}

	public void HandleHighlightObject(FrameworkDataElement element)
	{
		if (element is TradeLineData { go: not null, go: var go } tradeLineData)
		{
			string text = " {{K|" + go.WeightEach + "#}}";
			if (mode == TradeUI.TradeScreenMode.Trade)
			{
				string text2 = (go.IsCurrency ? "Y" : "B");
				string text3 = "{{" + text2 + "|$}}{{C|" + TradeUI.FormatPrice(TradeUI.GetValue(go, tradeLineData.traderInventory), CostMultiple) + "}}";
				text = text + " " + text3;
			}
			detailsRightText.SetText(text);
			detailsLeftText.SetText(go.DisplayNameSingle);
		}
		else
		{
			detailsLeftText.SetText("");
			detailsRightText.SetText("");
		}
	}

	public virtual void CheckLayoutFrame()
	{
		if (base.canvasGroup.enabled && (lastWidth != Screen.width || lastStageScale != Options.StageScale))
		{
			ResetLayoutFrame();
		}
	}

	public void Update()
	{
		if (layoutGroupFrame > 0)
		{
			layoutGroupFrame--;
			if (layoutGroupFrame <= 0 && componentsToEnableOnResize != null)
			{
				MonoBehaviour[] array = componentsToEnableOnResize;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].enabled = false;
				}
			}
		}
		CheckLayoutFrame();
		if (isCurrentWindow() && navigationContext.IsActive() && lastContext != NavigationController.instance.activeContext)
		{
			lastContext = NavigationController.instance.activeContext;
			UpdateMenuBars();
		}
	}

	public void MoveItem(TradeLineData data, int direction)
	{
	}
}
