#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class PortfolioManager : Indicator
	{
		private Account account;
		private Position position;
		private double realizedPnL;
		private double cashValue;
		private double OpenPnL;
		private double allPositions = 0.0;
		private double totalPercent = 0.0;
		private string  cashValuestring;
		string[] Ticker = {} ;
		int[] Percents = {};
		
		struct PostionData
		{
		    public string ticker;
		    public double percent;
			public double price;
		}
		
		
		
		List<PostionData> Portfolio = new List<PostionData>();
		List<PostionData> DesiredPortfolio = new List<PostionData>();
		List<PostionData> PositionAdjust = new List<PostionData>();
		string[] FoundTickers;
		List<PostionData> MissingTickers = new List<PostionData>();
		List<PostionData> CorrectTickers = new List<PostionData>();
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name										= "Portfolio Manager";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
				InitialInvestment					= 9200;
			}
			else if (State == State.Configure)
			{ }
			else if (State == State.DataLoaded)
			{			
				
				ClearOutputWindow();
				// Find our account
				lock (Account.All)
					account = Account.All.FirstOrDefault(a => a.Name == AccountName);

				// Subscribe to account item updates
				if (account != null)
				{
					account.AccountItemUpdate += OnAccountItemUpdate;
					account.PositionUpdate += OnPositionUpdate;
					
					foreach (Position pos in account.Positions)
						if (pos.Instrument == Instrument)
							position = pos;
					
					realizedPnL = account.Get(AccountItem.TotalCashBalance, Currency.UsDollar);
					cashValue = account.Get(AccountItem.CashValue, Currency.UsDollar);
				}
			}
			else if(State == State.Terminated)
			{
				// Make sure to unsubscribe to the account item subscription
        		if (account != null)
				{
            		account.AccountItemUpdate -= OnAccountItemUpdate;
					account.PositionUpdate -= OnPositionUpdate;
				}
			}
			
		}
		
		protected override void OnBarUpdate()
		{
			int Service = 0; // 0 aggressive, 1 income
		
			if ( Count - 2 == CurrentBar) {
				ChoosePortfilio(service: Service);
				DesiredPortfolioList(Ticker: Ticker, Percents: Percents);
				CreatePortfolio();
				ComparePortfolioTickers();
				DoesPercentageMatch();
			}
		}
		
		private void ChoosePortfilio(int service) {
			switch (service) {
				case 0:
					/// Aggressive Income 11%
					Print("Aggressive Income 11% Protfolio\n");
					Ticker = new string[] { "RYLD", "XYLD", "ETW", "SVOL" , "RA" , "CLM", "RIV", "GOF", "BSTZ", "AWP", "MVRL", "CEFD", "BDCX", "SLVO", "USOI" };
					Percents  = new int[] {12,12,6,6,10,5,8,7,5,5,4,4,8,4,2,2};
					break;
				case 1:
					///  Income 9% 7 correct
					Print("Income 9% Protfolio\n");
					Ticker = new string[]  { "QYLD", "RYLD", "XYLD", "YYY", "JEPI", "ETW", "SVOL", "RA", "CLM", "RIV", "GOF", "BGY", "AWP" };
					Percents  = new int[]  {12,12,10,7,8,8,7,7,5,7,6,6,5};
					break;
				default:
					break;	
			}
		}
		
		private void CreatePortfolio() {
			foreach (Position position in account.Positions)
			{ 
				OpenPnL = position.GetUnrealizedProfitLoss(PerformanceUnit.Points, Close[0]);
				string TickerName = (position.Instrument.FullName);
				double Quantities = position.Quantity;
				double PositionPrice = position.AveragePrice;
				double CurrentPrice = position.GetMarketPrice();
				
				double InitialAllocation = Quantities * PositionPrice;
				double PercentOfInitial =  (InitialAllocation / InitialInvestment) * 100;
				cashValuestring 		= cashValue.ToString("N2");
				PostionData positionData;
				positionData.ticker = TickerName;
				positionData.percent = PercentOfInitial;
				positionData.price = CurrentPrice;
				Portfolio.Add(positionData);
				allPositions += OpenPnL;
				totalPercent += PercentOfInitial;
				//Print(TickerName + " " + Quantities + " Shares " + Quantities + " Cash " + InitialAllocation );
			}
			Print("Cash: " + cashValuestring + " Account Value: $" + allPositions.ToString("N0") + " \ttotal percent: " + totalPercent.ToString("N0") + "%");
			ShowPositions(positions: Portfolio, name: "Current Positions");
		}
		
		private void ComparePortfolioTickers() {	
			CheckLists( list1: Portfolio, list2: DesiredPortfolio);
			ShowPositions(positions: MissingTickers, name: "Missing Positions");
			ShowPositions(positions: CorrectTickers, name: "Correct Positions");
		}
			
		private void CheckLists(List<PostionData> list1, List<PostionData> list2)
		{
		    foreach (var product2 in list2)
		    {
		        bool result = false;
		        foreach (var product in list1)
		        {
		            if (product.ticker == product2.ticker)
		            {
		                result = true;
						CorrectTickers.Add(product2);
		                break;
		            }
		        }
		        if (!result)
		        { 
					// assign cash value for % of portfolio
					ConvertPctToShares(position: product2);
					MissingTickers.Add(product2);
		        }
		    } 
		}
		
		private void ConvertPctToShares(PostionData position) {
			
			double PositionPercent = position.percent * 0.01;
			double InitialInvestAmt = InitialInvestment * PositionPercent;
			double NumShares =  InitialInvestAmt / position.price;
			
			Print("\nNew position to enter on " + position.ticker + 
			" \tBuy " + NumShares.ToString("N1") + " shares at $" + position.price.ToString("N2") + " for a total of $" + InitialInvestAmt.ToString("N0"));
			Print("Convert % to shares: " + " " + position.percent + 
			"% is an Initial Investment of $" + InitialInvestAmt.ToString("N0") +   " = $" + InitialInvestment +" * "+ PositionPercent.ToString("N3") + 
			" Number of Shares: " + NumShares.ToString("N1") +" = $"
				+  InitialInvestAmt.ToString("N0") +" / "+ position.price);
		}
		
		private void  DoesPercentageMatch() {
			Print("\nchecking percents...");
			foreach (var portfolio in Portfolio)
		    {
		        bool result = false;
		        foreach (var correct in CorrectTickers)
		        {
		            if (correct.ticker == portfolio.ticker)
		            {
						//Print(correct.ticker + " " +  portfolio.ticker);
						 if (correct.percent == portfolio.percent) {
							// Print(correct.percent + " matched " +  portfolio.percent);
						 } else {
							 
							 // difference to target percent
							 double diffToTargetPct = correct.percent  - portfolio.percent;
							 
							 // $ to target percent, pct * postfolio value
							 double cashDiffToTarget = InitialInvestment * ( diffToTargetPct * .01);
							 
							 // num shared to target pct
							 double NumSharesNeeded = cashDiffToTarget / portfolio.price;
							 Print("desired % on " + correct.ticker  + " is \t" + correct.percent + "\t != portfolio % of \t " +  portfolio.percent.ToString("N1") + " " +
							 "  Diff to target percent is " + diffToTargetPct.ToString("N1") + "%" + " $" + cashDiffToTarget.ToString("N0") + " " + 
							 " Shareds needed to trade " + NumSharesNeeded.ToString("N0")
							 );
						 }
		            
					}
				}
			}
		}
		
		private double ReturnPriceFor(string ticker) {
			double answer = 0.0;
			switch (ticker) {
				case "SVOL":
					answer = 22.07;
					break;
				case "GOF":
					answer = 16.31;
					break;
				case "AWP":
					answer = 4.36;
					break;
				case "YYY":
					answer = 12.39;
					break;
				case "JEPI":
					answer = 56.19;
					break;
				case "BGY":
					answer = 5.26;
					break;
				case "MVRL":
					answer = 23.49;
					break;
				case "BSTZ":
					answer = 17.76;
					break;
				case "CEFD":
					answer = 21.52;
					break;
				case "BDCX":
					answer = 23.49;
					break;
				case "SLVO":
					answer = 87.45;
					break;
				case "USOI":
					answer = 84.98;
					break;
				default:
					answer = 0.0;
					break;	
			}
			return answer;
		}
		
		private void DesiredPortfolioList(string[] Ticker, int[] Percents) {
			ReturnPriceFor( ticker: "SVOL") ;
			PostionData positionData;
			int count = 0;
			foreach (string t in Ticker)
			{
				positionData.ticker = t;
				positionData.percent = Percents[count];
				positionData.price = ReturnPriceFor( ticker: t);
				DesiredPortfolio.Add(positionData);
				count += 1;
			}
			ShowPositions(positions: DesiredPortfolio, name: "Desired Positions");
		}
		
		
		private void ShowPositions(List<PostionData> positions, string name) {
			Print("\n" + positions.Count + "  " + name );
			foreach (PostionData pos in positions)
			{
				Print(pos.ticker + ": " + pos.percent.ToString("N1") + "%" + " close: " + pos.price.ToString("N2"));
			}
		}
				
		
		private void OnAccountItemUpdate(object sender, AccountItemEventArgs e)
		{
			if (e.AccountItem == AccountItem.TotalCashBalance)
				realizedPnL = e.Value;
			if (e.AccountItem == AccountItem.CashValue)
				cashValue = e.Value;
		}
		
		private void OnPositionUpdate(object sender, PositionEventArgs e)
		{
			if (e.Position.Instrument == Instrument && e.MarketPosition == MarketPosition.Flat)
				position = null;
			else if (e.Position.Instrument == Instrument && e.MarketPosition != MarketPosition.Flat)
				position = e.Position;	
		}
		
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		public string AccountName { get; set; }

		public PerformanceUnit PerformanceUnit { get; set; }
		
		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="InitialInvestment", Order=1, GroupName="Parameters")]
		public int InitialInvestment
		{ get; set; }
		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private PortfolioManager[] cachePortfolioManager;
		public PortfolioManager PortfolioManager(int initialInvestment)
		{
			return PortfolioManager(Input, initialInvestment);
		}

		public PortfolioManager PortfolioManager(ISeries<double> input, int initialInvestment)
		{
			if (cachePortfolioManager != null)
				for (int idx = 0; idx < cachePortfolioManager.Length; idx++)
					if (cachePortfolioManager[idx] != null && cachePortfolioManager[idx].InitialInvestment == initialInvestment && cachePortfolioManager[idx].EqualsInput(input))
						return cachePortfolioManager[idx];
			return CacheIndicator<PortfolioManager>(new PortfolioManager(){ InitialInvestment = initialInvestment }, input, ref cachePortfolioManager);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.PortfolioManager PortfolioManager(int initialInvestment)
		{
			return indicator.PortfolioManager(Input, initialInvestment);
		}

		public Indicators.PortfolioManager PortfolioManager(ISeries<double> input , int initialInvestment)
		{
			return indicator.PortfolioManager(input, initialInvestment);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.PortfolioManager PortfolioManager(int initialInvestment)
		{
			return indicator.PortfolioManager(Input, initialInvestment);
		}

		public Indicators.PortfolioManager PortfolioManager(ISeries<double> input , int initialInvestment)
		{
			return indicator.PortfolioManager(input, initialInvestment);
		}
	}
}

#endregion
