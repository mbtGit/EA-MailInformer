using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace trackerMailInformer
{
    public static class Consts
    {
        public static string PREFIX = "withoutVolume";
        public static string COUNTER =                      PREFIX + "files\\counter.txt";
        public static string HISTORY =                      PREFIX + "files\\balanceHistory.txt";
        public static string RECOMMENDATIONS =              PREFIX + "files\\recommendations.txt";
        public static string DETAILS =                      PREFIX + "files\\strategiesDetails.txt";
        public static string CONTACTS =                     PREFIX + "files\\contacts.txt";
        public static string START_DATE =                   PREFIX + "files\\startdate.txt";
        public static string BID_ASK_SPREAD =               PREFIX + "files\\bidaskpercent.txt";
        public static string MONEY_START =                  PREFIX + "files\\moneyStart.txt";
        public static int   COUNT_OF_DISTRIBUTION_SLICES    = 20;
        public static int   COUNT_OF_QUANTILES              = 20;
        public static bool  WITH_QUANTILES_CHECK                    = true;
        public static bool  WITH_WINDOW_RETURNS_CHECK               = false;
        public static bool  WITH_WINDOW_RETURNS_CHECK_WHEN_SINGLE   = false;
        public static bool  WITH_VOLUME_WINDOW_INC_CHECK            = false; ////////////////////////////////////////////////////////////////
        public static bool  CONSTANT_INVESTMENT_AMOUNT              = false;

        public static int NUM_OF_THREADS = 4;

        public static double MONEY_START_AMMOUNT;
        public static double MINIMUM_NOISE = 0; //0.25;
        public static double MAXIMUM_NOISE = 0; //0.5;
        public static double MINIMUM_PRICE = 5;
        public static double MINIMUM_VOLUME = 250000;
        public static double MINIMUM_WINDOW_RETURN = 10;
        public static double PERCENT_OF_LOSS_STOPLOSS_LIMIT = -5; // 0 = no stop loss /////////////////////////////////////////////////////////
        public static double K_VOLUME_INC = 1.25;
        public static int WINDOW = 5;
        public static int PRE_WINDOW = 10;
        public static int PRE_PRE_WINDOW = 30;

        static Consts()
        {
            Consts.MONEY_START_AMMOUNT = double.Parse(File.ReadAllText(Consts.MONEY_START));
        }
    }
    public class MyRequersts
    {
        private static readonly HttpClient client = new HttpClient();

        static MyRequersts()
        {
            //client.Timeout = new TimeSpan(0, 1, 0);
        }


        public static async Task<bool> loop()
        {
            //client.Timeout = new TimeSpan(0, 1, 0);
            DateTime d = new DateTime(2017, 3, 1);
            while (d < DateTime.Now)
            {
                StringContent content = new StringContent("{ \"date\" : \"" + d.ToString("yyyy-MM-dd") + "\"}", Encoding.UTF8, "application/json");
                string str = "https://shapira.herokuapp.com/getSymbolsByDate";
                Console.WriteLine(await getHTTP(str, content));
                d = d.AddDays(1);
            }
            return true;
        }
        public static async Task<string> getHTTP(string strURL, StringContent content)
        {
            using (var response = await client.PostAsync(strURL, content))
            {
                return await (response.Content.ReadAsStringAsync());
            }
        }
        public static async Task<string> getHTTP(string strURL)
        {
            using (var response = await client.GetAsync(strURL))
            {
                return await (response.Content.ReadAsStringAsync());
            }
        }
    }
    public class Price
    {
        public double price;
        public double ask = -1;
        public double bid = -1;

        public Price(double dPrice, double dAsk, double dBid)
        {
            this.price = dPrice;
            this.ask = dAsk;
            this.bid = dBid;
        }
    }
    public class Share
    {
        public Price open;
        public Price close;
        public Price high;
        public Price low;
        public DateTime date;
        public double windowReturn;
        public long volume;
        public string symbol;
        public int direction;

        
        public static async Task<List<Share>> getSharesInfo(string strSymbol, int nNumOfDays, DateTime dtFirstDate)
        {
            List<Share> ret = new List<Share>();
            int nTrys = 0;
            string[] days;

            while (ret.Count == 0 && nTrys < 3)
            {
                dtFirstDate = new DateTime(dtFirstDate.Year, dtFirstDate.Month, dtFirstDate.Day);

                DateTime date = new DateTime(2100, 1, 1);
                string html = await MyRequersts.getHTTP(string.Format("https://finance.yahoo.com/quote/{0}/history?", strSymbol));
                DateTime start = new DateTime(1970, 1, 1, 0, 0, 0);
                string strToFind = "\"HistoricalPriceStore\":{\"prices\":";
                string a = "{\"date\":";
                string b = "\"open\":";
                string c = "\"close\":";
                string d = "\"volume\":";
                string e = "\"high\":";
                string f = "\"low\":";

                int nStartIndex = html.IndexOf(strToFind);
                html = html.Substring(strToFind.Length + nStartIndex, html.Length - nStartIndex - strToFind.Length);
                int nEndIndex = html.IndexOf(']');
                html = html.Substring(0, nEndIndex);
                days = html.Split('}');

                for (int nDayIndex = 0; nDayIndex < days.Length; nDayIndex++)
                {
                    string[] values = null;

                    try
                    {
                        values = days[nDayIndex].Remove(0, 1).Split(',');
                        long lDate = long.Parse(values[0].Remove(0, a.Length));
                        date = start.AddMilliseconds(lDate * 1000);
                        date = new DateTime(date.Year, date.Month, date.Day);
                    }
                    catch { };

                    if (date <= dtFirstDate)
                    {
                        for (int nIndex = 0; (nIndex < nNumOfDays) && (nIndex + nDayIndex < days.Length - 1); nIndex++)
                        {
                            try
                            {
                                values = days[nIndex + nDayIndex].Remove(0, 1).Split(',');

                                long lDate = long.Parse(values[0].Remove(0, a.Length));
                                date = start.AddMilliseconds(lDate * 1000);
                                date = new DateTime(date.Year, date.Month, date.Day);
                                double dOpen = double.Parse(values[1].Remove(0, b.Length));
                                double dHigh = double.Parse(values[2].Remove(0, e.Length));
                                double dLow = double.Parse(values[3].Remove(0, f.Length));
                                double dClose = double.Parse(values[4].Remove(0, c.Length));
                                long lVolume = long.Parse(values[5].Remove(0, d.Length));


                                Share s = new Share();
                                s.date = date;
                                s.close = new Price(dClose, -1, -1);
                                s.high = new Price(dHigh, -1, -1);
                                s.low = new Price(dLow, -1, -1);
                                s.open = new Price(dOpen, -1, -1);
                                s.volume = lVolume;
                                s.symbol = strSymbol;
                                ret.Add(s);
                            }
                            catch (Exception err)
                            {
                                if (err.Message.Contains("format"))
                                {
                                    nNumOfDays++;
                                }
                                else
                                {
                                    int sn = 3;
                                }
                            };
                        }

                        break;
                    }
                }

                nTrys++;
                if (nTrys == 3 && ret.Count == 0)
                    Console.Write(days.Length + " Lines for :    ");
            }

            return ret;
        }
        public static async Task<Share> getRealTimeData(string strSymbol)
        {
            string html = await MyRequersts.getHTTP(string.Format("https://finance.yahoo.com/quote/{0}", strSymbol));

            Share s = new Share();
            s.close = new Price(
                yahooFilterValueFrom(html, "Currency in USD"),
                yahooFilterValueFrom(html, "ASK-value"),
                yahooFilterValueFrom(html, "BID-value"));

            s.volume = (long)yahooFilterValueFrom(html, "VOLUME-value");
            if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday || s.volume == s.close.ask || s.volume == s.close.bid)
            {
                s.close.ask = s.close.price;
                s.close.bid = s.close.price;
            }

            s.open = new Price(s.close.price, s.close.ask, s.close.bid);
            s.high = new Price(s.close.price, s.close.ask, s.close.bid);
            s.low = new Price(s.close.price, s.close.ask, s.close.bid);

            s.symbol = strSymbol;

            return s;
        }
        public static double yahooFilterValueFrom(string strHtml, string strFirstString)
        {
            double ret = -1;

            try
            {
                int a = strHtml.IndexOf(strFirstString);
                if (a != -1)
                {
                    int b = strHtml.IndexOf("-->", a);
                    if (b != -1)
                    {
                        int c = strHtml.IndexOf("<!--", b + 3);
                        if (c != -1)
                        {
                            if (strFirstString == "Currency in USD")
                            {
                                while ((c - (b + 3) == 0) || (c - (b + 3) > 10))
                                {
                                    b = strHtml.IndexOf("-->", c);
                                    c = strHtml.IndexOf("<!--", b + 3);
                                }
                            }

                            string str = strHtml.Substring(b + 3, c - (b + 3));
                            ret = double.Parse(str.Split(' ')[0]);
                        }
                    }
                }
            }
            catch
            { };

            return ret;
        }
    }
    public class Data
    {
        public int direction;
        public double lastPrice;
        public double windowReturn;
        public double ratio;
        public int quantile = -1;
        public string symbol;
        public Data(string str, int nDirection, double dWindowReturn, double dLastPrice)
        {
            this.symbol = str;
            this.direction = nDirection;
            this.windowReturn = dWindowReturn;
            this.lastPrice = dLastPrice;
        }
    }
    public class Recommendation
    {
        public static Dictionary<DateTime, ConcurrentDictionary<string, int>> forward = new Dictionary<DateTime, ConcurrentDictionary<string, int>>();

        public DateTime date;
        public ConcurrentDictionary<string, Data> symbols;

        public Recommendation()
        {
            symbols = new ConcurrentDictionary<string, Data>();
        }
        public void write()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string curr in symbols.Keys)
                sb.AppendFormat("{0};{1};{2};{3};{4};{5}\n", date.ToShortDateString(), curr, symbols[curr].direction, symbols[curr].quantile, symbols[curr].windowReturn, symbols[curr].lastPrice);

            File.AppendAllText(Consts.RECOMMENDATIONS, sb.ToString());
        }

        public async Task<Object> predictTomorrowSymbols(DateTime TODAY)
        {
            int nThreads = 0;
            ConcurrentDictionary<string, int> lstForward = new ConcurrentDictionary<string, int>();
            DateTime dtRef = new DateTime(1970, 1, 1, 0, 0, 0);
            DateTime dtTomorrow = new DateTime(
                TODAY.AddDays(1).Year,
                TODAY.AddDays(1).Month,
                TODAY.AddDays(1).Day, 0, 0, 0);

            try
            {
                string response = await MyRequersts.getHTTP(string.Format("https://www.zacks.com/includes/classes/z2_class_calendarfunctions_data.php?calltype=eventscal&date={0}&type=1",
                    ((dtTomorrow.Ticks - dtRef.Ticks) / 10000000 + 22000)));  //1509685200  1509598800
                


                string[] lines = response.Split('[');
                //Console.WriteLine("EARNINGS IN ZACKS count " + (lines.Length - 2));
                foreach (var currShareLine in lines)
                {
                    int n = currShareLine.IndexOf("alt=\\\"");
                    if (n != -1)
                    {
                        int nSymbolIndex = n + "alt=\\\"".Length;
                        int nEndSymbolIndex = currShareLine.IndexOf(" ", nSymbolIndex);

                        string symbol = currShareLine.Substring(nSymbolIndex, nEndSymbolIndex - nSymbolIndex);


                        int nOffset = currShareLine.Contains("\"amc\"") ? 1 : (currShareLine.Contains("\"bmo\"") ? 0 : 0);
                        int nStart = currShareLine.IndexOf("<div class");

                        #region estimate
                        double dEstimate = -999;
                        if (nStart != -1)
                        {
                            string[] split = currShareLine.Substring(0, nStart).Split(',');
                            int i;
                            int offset = 0;

                            for (i = 0; i < split.Length - 2; i++)
                                offset += split[i].Replace(" ", string.Empty).StartsWith("\"") ? 0 : 1;

                            string strEstimate = split[4 + offset].Remove(0, 2);
                            strEstimate = strEstimate.Remove(strEstimate.Length - 1, 1);
                            double.TryParse(strEstimate, out dEstimate);
                        }
                        #endregion

                        if (nThreads == Consts.NUM_OF_THREADS)
                        {
                            while (nThreads > 0)
                                Thread.Sleep(100);
                        }
                        Interlocked.Increment(ref nThreads);

                        ThreadPool.QueueUserWorkItem(async (stateInfo) => {
                            //new Thread(() => {


                            // Set prediction
                            List<Share> info = null;
                            List<Share> window = new List<Share>();
                            List<Share> volumeWindow = new List<Share>();
                            List<Share> pre = new List<Share>();

                            try
                            {
                                info = await Share.getSharesInfo(symbol, Consts.WITH_VOLUME_WINDOW_INC_CHECK ? Consts.PRE_PRE_WINDOW : Consts.WINDOW, new DateTime(TODAY.Year, TODAY.Month, TODAY.Day));
                                for (int nWindowIndex = 0; nWindowIndex < Consts.WINDOW; nWindowIndex++) { window.Add(info[nWindowIndex]); }
                                if (Consts.WITH_VOLUME_WINDOW_INC_CHECK)
                                {
                                    for (int nWindowIndex = 2; nWindowIndex < Consts.PRE_WINDOW; nWindowIndex++) { volumeWindow.Add(info[nWindowIndex]); }
                                    for (int nWindowIndex = Consts.PRE_WINDOW; nWindowIndex < Consts.PRE_PRE_WINDOW; nWindowIndex++) { pre.Add(info[nWindowIndex]); }
                                }
                                double windowReturn = window[0].close.price / window[window.Count - 1].open.price;
                                int windowDirection = windowReturn >= 1 ? 1 : -1;
                                double avgVolume = pre.Count == 0 ? 0 : pre.Average(s => s.volume);
                                bool bAboveAVGVolume = !Consts.WITH_VOLUME_WINDOW_INC_CHECK || (volumeWindow.Average(S => S.volume) > avgVolume * Consts.K_VOLUME_INC);

                                if (nOffset == 0)
                                {
                                    if (window[0].volume >= Consts.MINIMUM_VOLUME && bAboveAVGVolume)
                                        symbols.TryAdd(symbol, new Data(symbol, (-1) * windowDirection, windowReturn, window[0].close.price));
                                }
                                else
                                {
                                    lstForward.TryAdd(symbol, 0);
                                }
                            }
                            catch (Exception e)
                            {
                                if (e.Message.Contains("out of range"))
                                {
                                    Console.WriteLine(symbol + " - " + (info != null ? info.Count : 0));
                                }
                                else
                                {
                                    Console.WriteLine(symbol + " - " + e);
                                    int ns = 3;
                                }
                            }
                            finally
                            {
                                Interlocked.Decrement(ref nThreads);
                            }

                        });//.Start();
                    }
                }

                while (nThreads > 0)
                    Thread.Sleep(1000);
            }
            catch (Exception e) { symbols.Clear(); }




            if (lstForward.Count != 0)
            {
                DateTime dtFarward = dtTomorrow.AddDays(1);
                if (dtFarward.DayOfWeek == DayOfWeek.Saturday)
                    dtFarward = dtFarward.AddDays(1);
                if (dtFarward.DayOfWeek == DayOfWeek.Sunday)
                    dtFarward = dtFarward.AddDays(1);

                forward.Add(dtTomorrow.AddDays(1), lstForward);
            }
            if (forward.ContainsKey(dtTomorrow))
            {
                foreach (string symbol in forward[dtTomorrow].Keys)
                {
                    if (nThreads == Consts.NUM_OF_THREADS)
                    {
                        while (nThreads > 0)
                            Thread.Sleep(100);
                    }
                    Interlocked.Increment(ref nThreads);
                    
                    ThreadPool.QueueUserWorkItem(async (stateInfo) =>
                    {
                        //new Thread(() => {

                        List<Share> info = null;
                        List<Share> window = new List<Share>();
                        List<Share> volumeWindow = new List<Share>();
                        List<Share> pre = new List<Share>();

                        try
                        {
                            info = await Share.getSharesInfo(symbol, Consts.WITH_VOLUME_WINDOW_INC_CHECK ? Consts.PRE_PRE_WINDOW : Consts.WINDOW, new DateTime(TODAY.Year, TODAY.Month, TODAY.Day));
                            for (int nWindowIndex = 0; nWindowIndex < Consts.WINDOW; nWindowIndex++) { window.Add(info[nWindowIndex]); }
                            if (Consts.WITH_VOLUME_WINDOW_INC_CHECK)
                            {
                                for (int nWindowIndex = 2; nWindowIndex < Consts.PRE_WINDOW; nWindowIndex++) { volumeWindow.Add(info[nWindowIndex]); }
                                for (int nWindowIndex = Consts.PRE_WINDOW; nWindowIndex < Consts.PRE_PRE_WINDOW; nWindowIndex++) { pre.Add(info[nWindowIndex]); }
                            }
                            double windowReturn = window[0].close.price / window[window.Count - 1].open.price;
                            int windowDirection = windowReturn >= 1 ? 1 : -1;
                            double avgVolume = pre.Count == 0 ? 0 : pre.Average(s => s.volume);
                            bool bAboveAVGVolume = !Consts.WITH_VOLUME_WINDOW_INC_CHECK || (volumeWindow.Average(S => S.volume) > avgVolume * Consts.K_VOLUME_INC);

                            if (window[0].volume >= Consts.MINIMUM_VOLUME && bAboveAVGVolume)
                                symbols.TryAdd(symbol, new Data(symbol, (-1) * windowDirection, windowReturn, window[0].close.price));
                        }
                        catch (Exception e)
                        {
                            if (e.Message.Contains("out of range"))
                            {
                                Console.WriteLine(symbol + " - " + (info != null ? info.Count : 0));
                            }
                            else
                            {
                                Console.WriteLine(symbol + " - " + e);
                                int ns = 3;
                            }
                        }
                        finally
                        {
                            Interlocked.Decrement(ref nThreads);
                        }

                    });//.Start();
                }


                while (nThreads > 0)
                    Thread.Sleep(1000);
            }

            return new Object();
        }
        public void minimizePriceWindowAndQuantiles(bool bWitQuantilesCheck, bool bWithWindowReturnsCheck, bool bWithWindowReturnsCheckWhenSingle, double minPirce, double upperThanReturn)
        {
            double[] arrValues = new double[Consts.COUNT_OF_DISTRIBUTION_SLICES + 1];
            double[] arrDestrebutions = new double[Consts.COUNT_OF_DISTRIBUTION_SLICES + 1];
            double dMin = double.MaxValue;
            double dMax = double.MinValue;
            upperThanReturn = upperThanReturn / 100;

            // Set returns in window
            foreach (Data curr in this.symbols.Values)
            {
                if (curr.windowReturn < dMin) dMin = curr.windowReturn;
                if (curr.windowReturn > dMax) dMax = curr.windowReturn;
            }

            // Set values ranges
            arrValues[0] = dMin;
            for (int i = 1; i < arrValues.Length; i++)
                arrValues[i] = arrValues[i - 1] + ((dMax - dMin) / Consts.COUNT_OF_DISTRIBUTION_SLICES);

            // Find value range cell
            foreach (Data d in this.symbols.Values)
            {
                int i;
                for (i = 0; ((i < arrValues.Length) && (d.windowReturn >= arrValues[i])); i++) ;
                arrDestrebutions[i - 1]++;
                d.quantile = i - 1;
            };

            // Set destribution and quantiles
            arrDestrebutions[0] = arrDestrebutions[0] / this.symbols.Values.Count;
            for (int i = 1; i < arrDestrebutions.Length; i++)
            {
                arrDestrebutions[i] = arrDestrebutions[i - 1] + (arrDestrebutions[i] / this.symbols.Values.Count);
                arrDestrebutions[i - 1] = (int)((arrDestrebutions[i - 1] / (1.0 / Consts.COUNT_OF_QUANTILES)) - 0.0001);
            }
            arrDestrebutions[arrDestrebutions.Length - 1] = (int)((arrDestrebutions[arrDestrebutions.Length - 1] / (1.0 / Consts.COUNT_OF_QUANTILES)) - 0.0001);

            // Set final quantils
            foreach (var d in this.symbols.Values) { d.quantile = (int)arrDestrebutions[d.quantile]; };


            // Upper than
            List<string> lstToRemove = new List<string>();
            foreach (string symbol in this.symbols.Keys)
            {
                if ((this.symbols[symbol].lastPrice < minPirce) ||
                    ((bWitQuantilesCheck) && (this.symbols[symbol].quantile != 0) && (this.symbols[symbol].quantile != Consts.COUNT_OF_QUANTILES - 1)) ||
                    ((bWithWindowReturnsCheck) && (this.symbols[symbol].windowReturn < 1 + upperThanReturn) && (this.symbols[symbol].windowReturn > 1 - upperThanReturn)))
                    lstToRemove.Add(symbol);

                if ((this.symbols[symbol].windowReturn == 1) ||
                    ((bWithWindowReturnsCheckWhenSingle) && (this.symbols.Count == 1) && (this.symbols[symbol].windowReturn < 1 + upperThanReturn) && (this.symbols[symbol].windowReturn > 1 - upperThanReturn)))
                    lstToRemove.Add(symbol);
            }

            Data dref;
            foreach (string symbol in lstToRemove)
                this.symbols.TryRemove(symbol, out dref);
        }
    }
    public class Strategy
    {
        public static List<Strategy> allStrategies = new List<Strategy>();
        public double MONEY = Consts.MONEY_START_AMMOUNT;
        public double LAST_CHANGE = 0;

        public double commissionPerShare;
        public double commissionMinimum;
        public double commissionFixed;
        public strategies strategy;
        public string details;

        static Strategy()
        {
            int nIndex = 0;
            foreach (string line in File.ReadAllLines(Consts.DETAILS))
            {
                string[] split = line.Split(';');
                Strategy s = new Strategy();
                s.strategy = (strategies)nIndex++;
                s.details = split[1];
                s.commissionPerShare = double.Parse(split[2]);
                s.commissionMinimum = double.Parse(split[3]);
                s.commissionFixed = double.Parse(split[4]);
                allStrategies.Add(s);
            }
        }

        public static DataTable makeStrategiesTable(bool bwithInfo)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("");
            for (int nIndex = 0; nIndex < allStrategies.Count; nIndex++)
                dt.Columns.Add(((strategies)nIndex).ToString());

            if (bwithInfo)
                dt.Columns.Add("info");

            return dt;
        }
    }
    public enum strategies
    {
        Ally,
        None
    }




    class Program
    {
        public static SortedList<DateTime, Dictionary<strategies, double>> history = new SortedList<DateTime, Dictionary<strategies, double>>();
        public static List<string> strategiesDetails = new List<string>();
        public static DateTime TODAY = DateTime.Now;
        public static DateTime START = new DateTime(2017,2,1,16,0,0);
        public static bool bIsPresent = false;
        public static int nIsPresent = 0;
        static void Main(string[] args)
        {
            ThreadPool.SetMaxThreads(4 * Consts.NUM_OF_THREADS, 4 * Consts.NUM_OF_THREADS);
            AsyncContext.Run(() => MainAsync(args));
        }
        static async void MainAsync(string[] args)
        {
            START = DateTime.Parse(File.ReadAllText(Consts.START_DATE));
            Recommendation recommendationForTommorow = new Recommendation();
            TODAY = START;

            // EST TIMES 
            // Sunday       Closed
            // Monday       9:30 AM - 4:00 PM
            // Tuesday      9:30 AM - 4:00 PM
            // Wednesday    9:30 AM - 4:00 PM
            // Thursday     9:30 AM - 4:00 PM
            // Friday       9:30 AM - 4:00 PM
            // Saturday     Closed
            while (true)
            {
                Console.WriteLine("\n\n--------------------------------------------------------------\nToday is {0}, we are expecting for {1} positions PL", TODAY.ToShortDateString(), recommendationForTommorow.symbols.Count);


                // Get todays returns
                List<Share> todayInfo = null;

                if (!bIsPresent)
                {
                    todayInfo = getTodayInfo(recommendationForTommorow.symbols);
                }
                else
                {
                    TODAY = nIsPresent == 1 ? DateTime.Now : waitForTime(15, 59);
                    ConcurrentDictionary<string, Share> startOfTheDay = getTodayBidAndAsk(recommendationForTommorow.symbols);
                    foreach (string symbol in startOfTheDay.Keys) Console.WriteLine("Open price of {0}: {1}", symbol, startOfTheDay[symbol].open.price);
                    TODAY = waitForTime(15, 52);
                    ConcurrentDictionary<string, Share> endOfTheDay = getTodayBidAndAsk(recommendationForTommorow.symbols);
                    todayInfo = combineData(startOfTheDay, endOfTheDay);
                }

                Dictionary<strategies, Dictionary<string, string>> today = calculateBalancePerStrategy(todayInfo, Consts.MINIMUM_NOISE, Consts.MAXIMUM_NOISE);

                // New recommendationForTommorow
                recommendationForTommorow = new Recommendation();
                recommendationForTommorow.date = TODAY.AddDays(1);
                await recommendationForTommorow.predictTomorrowSymbols(TODAY);
                recommendationForTommorow.minimizePriceWindowAndQuantiles(Consts.WITH_QUANTILES_CHECK, Consts.WITH_WINDOW_RETURNS_CHECK, Consts.WITH_WINDOW_RETURNS_CHECK_WHEN_SINGLE, Consts.MINIMUM_PRICE, Consts.MINIMUM_WINDOW_RETURN);
                recommendationForTommorow.write();
                Console.WriteLine("Buy {0} positions this night for tomorrow", recommendationForTommorow.symbols.Count);

                StringBuilder strMessage = new StringBuilder(string.Format("{0}<body dir='ltr'><h1>{1} Returns report - {2}</h1><br>", style, Consts.PREFIX, TODAY.ToShortDateString()));
                strMessage.Append(makeTodaysReturnsTable(today));
                strMessage.Append(makeTomorrowPredictedTable(recommendationForTommorow.symbols));
                strMessage.Append(makeHistoryTable());
                strMessage.Append("</body>");

                if (todayInfo.Count > 0 || recommendationForTommorow.symbols.Count > 0)
                    sendReportMails(strMessage.ToString());
                
                

                if (!bIsPresent)
                {
                    if ((DateTime.Now.Hour > 8 && TODAY < DateTime.Now.AddDays(-1)) || (DateTime.Now.Hour <= 8 && TODAY < DateTime.Now.AddDays(-2)))
                        TODAY = TODAY.AddDays(1);
                    else
                    {
                        bIsPresent = true;
                    }
                }
                
                if (bIsPresent)
                    nIsPresent++;
            }
        }


        public static DateTime waitForTime(int nHour, int nMinute)
        {
            Console.WriteLine("Time NOW IS: {0} , waiting until: {1}:{2}", DateTime.Now.ToShortTimeString(), nHour, nMinute);
            while (!((DateTime.Now.Hour == nHour) && (DateTime.Now.Minute == nMinute)))
                Thread.Sleep(60000);

            return DateTime.Now;
        }
       
        public static List<Share> getTodayInfo(ConcurrentDictionary<String, Data> symbols)
        {
            int nThreads = 0;
            ConcurrentBag<Share> lst = new ConcurrentBag<Share>();
            DateTime today = new DateTime(TODAY.Year, TODAY.Month, TODAY.Day);

            foreach (string strCurrSymbol in symbols.Keys)
            {                
                if (nThreads == Consts.NUM_OF_THREADS)
                {
                    while (nThreads > 0)
                        Thread.Sleep(100);
                }
                Interlocked.Increment(ref nThreads);

                ThreadPool.QueueUserWorkItem(async (stateInfo) =>
                {
                    //new Thread(() => {

                    try
                    {
                        List<Share> towDays = await Share.getSharesInfo(strCurrSymbol, 2, today);
                        Share s = towDays[0];
                        s.open = towDays[1].close;
                        s.windowReturn = symbols[strCurrSymbol].windowReturn;
                        s.direction = symbols[strCurrSymbol].direction;
                        if (s.date.CompareTo(today) == 0)
                            lst.Add(s);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(strCurrSymbol + " - FuckingProblam! \n" + e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref nThreads);
                    }


                });//.Start();
            }


            while (nThreads > 0)
                Thread.Sleep(1000);


            return lst.ToList<Share>();
        }
        public static ConcurrentDictionary<string,Share> getTodayBidAndAsk(ConcurrentDictionary<String, Data> symbols)
        {
            ConcurrentDictionary<string, Share> ret = new ConcurrentDictionary<string, Share>();

            Parallel.ForEach(symbols.Values, async (curr) => { 
            
                Share s = await Share.getRealTimeData(curr.symbol);
                s.windowReturn = curr.windowReturn;
                s.direction = curr.direction;
                s.date = DateTime.Now;
                ret.TryAdd(s.symbol, s);
            });

            return ret;
        }
        public static List<Share> combineData(ConcurrentDictionary<string, Share> startOfDay, ConcurrentDictionary<string, Share> endOfDay)
        {
            foreach (Share curr in startOfDay.Values)
                curr.close = endOfDay[curr.symbol].close;

            return startOfDay.Values.ToList<Share>();
        }
        public static Dictionary<strategies, Dictionary<string, string>> calculateBalancePerStrategy(List<Share> sharesInfo, double dMinNoise, double dMaxNoise)
        {
            Dictionary<strategies, Dictionary<string, string>> ret = new Dictionary<strategies, Dictionary<string, string>>();
            Random r = new Random((int)(DateTime.Today.Ticks % 1000));
            double dWindowsReturnInterval = sharesInfo.Sum(U => Math.Abs(U.windowReturn * 100 - 100));
            bool bFirstNoteNoneStrategyOver = true;
            foreach (Strategy currStrategy in Strategy.allStrategies)
            {
                Dictionary<string, string> dic = new Dictionary<string, string>();
                ret.Add(currStrategy.strategy, dic);
                double dSum = 0;

                foreach (Share currShare in sharesInfo)
                {
                    double dRatio = (Math.Abs(currShare.windowReturn * 100 - 100) / dWindowsReturnInterval);
                    double dPositionsStartValue = (Consts.CONSTANT_INVESTMENT_AMOUNT ? (currStrategy.MONEY < Consts.MONEY_START_AMMOUNT ? currStrategy.MONEY : Consts.MONEY_START_AMMOUNT) : currStrategy.MONEY) * dRatio;
                    int nCountOfShares = (int)(dPositionsStartValue / currShare.open.price) + 1;

                    double dAvragePrice = (currShare.close.price + currShare.open.price) / 2;
                    double dBidAskSpread = dAvragePrice * double.Parse(File.ReadAllText(Consts.BID_ASK_SPREAD)) * currShare.direction;
                    double change = (((currShare.close.price - dBidAskSpread) / (currShare.open.price + dBidAskSpread)) * 100 - 100) * currShare.direction;

                    if (currShare.close.ask != -1 &&
                        currShare.close.bid != -1 &&
                        currShare.open.ask != -1 &&
                        currShare.open.bid != -1)
                    {
                        change = currShare.direction > 0 ?
                            ((currShare.close.bid / currShare.open.ask) * 100 - 100) * currShare.direction :
                            ((currShare.close.ask / currShare.open.bid) * 100 - 100) * currShare.direction;
                    }


                    if (Consts.PERCENT_OF_LOSS_STOPLOSS_LIMIT != 0)
                    {
                        // dBidAskSpread is signed accoarding the direction!!!
                        double dLongPosition_OpenToLow = (((currShare.low.price - dBidAskSpread) / (currShare.open.price + dBidAskSpread)) * 100 - 100) * 1;
                        double dShortPosition_OpenToHigh = (((currShare.high.price - dBidAskSpread) / (currShare.open.price + dBidAskSpread)) * 100 - 100) * -1;
                        if (((currShare.direction == 1) && (dLongPosition_OpenToLow <= Consts.PERCENT_OF_LOSS_STOPLOSS_LIMIT)) ||
                            ((currShare.direction == -1) && (dShortPosition_OpenToHigh <= Consts.PERCENT_OF_LOSS_STOPLOSS_LIMIT)))
                        {
                            File.AppendAllText(Consts.PREFIX + "files\\log.txt", string.Format("{0} - {1} | Catch stopLoss {2}% insted of {3}%          | {4}%\n", 
                                currShare.date.ToShortDateString(), 
                                currShare.symbol, 
                                Consts.PERCENT_OF_LOSS_STOPLOSS_LIMIT,
                                Math.Round(change, 3),
                                Math.Round((currShare.direction == 1) ? dLongPosition_OpenToLow : dShortPosition_OpenToHigh), 3));

                            change = Consts.PERCENT_OF_LOSS_STOPLOSS_LIMIT;
                        }
                    }

                    // Noise on the change
                    change = currStrategy.strategy == strategies.None ? change : change - (Math.Abs(change) * (dMinNoise + ((dMaxNoise - dMinNoise) * r.NextDouble())));
                    change = (100 + change) / 100;

                    double dTotalCommition = nCountOfShares * currStrategy.commissionPerShare;
                    dTotalCommition = dTotalCommition < currStrategy.commissionMinimum ? currStrategy.commissionMinimum : dTotalCommition;
                    dTotalCommition = currStrategy.commissionFixed != 0 ? currStrategy.commissionFixed : dTotalCommition;

                    double dPL = ((dPositionsStartValue - dTotalCommition) * change) - dTotalCommition;
                    dSum += dPL;

                    if (bFirstNoteNoneStrategyOver && (currStrategy.strategy != strategies.None))
                        File.AppendAllText(Consts.COUNTER, string.Format("{0};{1};{2};{3};{4}\n", currShare.direction == 1 ? 1 : 0, change > 1 ? 1 : 0, currShare.symbol, nCountOfShares, dTotalCommition));

                    string extra = extra = string.Format("Open    : {0}<br>Open Bid: {1}<br>Open Ask: <b>{2}</b><br>Close    : {3}<br>Close Bid: <b>{4}</b><br>Close Ask: {5}<br>Volume   : {6}",
                            Math.Round(currShare.open.price, 2), 
                            (currShare.open.bid == -1 ? "" : Math.Round(currShare.open.bid, 2).ToString()),
                            (currShare.open.ask == -1 ? "" : Math.Round(currShare.open.ask, 2).ToString()),
                            Math.Round(currShare.close.price, 2),
                            (currShare.close.bid == -1 ? "" : Math.Round(currShare.close.bid, 2).ToString()),
                            (currShare.close.ask == -1 ? "" : Math.Round(currShare.close.ask, 2).ToString()),
                            currShare.volume);

                    dic.Add((currShare.direction > 0 ? "(Long) " : "(Short) ") + currShare.symbol, string.Format("{0};{1}",(dPL / dPositionsStartValue) * 100 - 100, extra));
                }

                currStrategy.LAST_CHANGE = sharesInfo.Count > 0 ? ((dSum / currStrategy.MONEY) * 100 - 100) : 0;
                currStrategy.MONEY = sharesInfo.Count > 0 ? dSum : currStrategy.MONEY;
                if (currStrategy.strategy != strategies.None)
                    bFirstNoteNoneStrategyOver = false;
            }

            return ret;
        }
     





        public static void sendReportMails(string messageBody)
        {
            string from = "orshapira91@gmail.com";
            SmtpClient client = new SmtpClient("smtp.gmail.com", 587);
            client.Credentials = new System.Net.NetworkCredential("orshapira91@gmail.com", "574phiakeu;2892");
            client.EnableSsl = true;

            foreach (string to in File.ReadAllLines(Consts.CONTACTS))
            {
                if (!to.StartsWith("//"))
                {
                    MailMessage message = new MailMessage(from, to);
                    message.Subject = string.Format("{0} Returns report {1}", Consts.PREFIX, TODAY.ToShortDateString());
                    message.Body = messageBody;
                    message.IsBodyHtml = true;

                    try
                    {
                        client.Send(message);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Mail was not been send");
                    }
                }
            }
        }





        public static string makeTomorrowPredictedTable(ConcurrentDictionary<string, Data> symbols)
        {
            StringBuilder sb = new StringBuilder("<br><br><br><u><b>Positions for tomorrow:</b></u><br>");
            double dWindowsReturnInterval = symbols.Values.Sum(U => Math.Abs(U.windowReturn * 100 - 100));


            foreach (string currSymbol in symbols.Keys)
            {
                sb.AppendFormat("<li><b>{0}</b> - <font color='{1}'>{2}</font> ({3}% of protfolio)</li>",
                    currSymbol,
                    symbols[currSymbol].direction > 0 ? "green" : "red",
                    symbols[currSymbol].direction > 0 ? "Long" : "Short",
                    Math.Round((Math.Abs(symbols[currSymbol].windowReturn * 100 - 100) / dWindowsReturnInterval) * 100,2));
            }

            return sb.ToString();
        }
        public static string makeTodaysReturnsTable(Dictionary<strategies, Dictionary<string, string>> results)
        {
            Dictionary<strategies, double> todaySumHistoryDic = new Dictionary<strategies, double>();
            history.Add(TODAY, todaySumHistoryDic);
            DataTable dt = Strategy.makeStrategiesTable(true);
            DataRow dr = null;

            foreach (string symbol in results[strategies.None].Keys)
            {
                dr = dt.NewRow();
                dt.Rows.Add(dr);
                dr[0] = symbol;
            }
            dr = dt.NewRow();
            dt.Rows.Add(dr);
            dr[0] = "<b>AVG</b>";


            if (dt.Rows.Count > 1)
            {
                for (int nStrategyIndex = 0; nStrategyIndex < results.Count; nStrategyIndex++)
                {
                    for (int nRowIndex = 0; nRowIndex < dt.Rows.Count - 1; nRowIndex++)
                    {
                        string[] info = results[(strategies)nStrategyIndex][(string)dt.Rows[nRowIndex][0]].Split(';');
                        double dReturn = double.Parse(info[0]);

                        dt.Rows[nRowIndex][nStrategyIndex + 1] = Math.Round(dReturn ,2).ToString() + "%";

                        if (nStrategyIndex == results.Count - 1)
                        {
                            dt.Rows[nRowIndex][nStrategyIndex + 2] = info[1];
                        }
                    }

                    todaySumHistoryDic.Add((strategies)nStrategyIndex, Strategy.allStrategies[nStrategyIndex].MONEY);
                    dt.Rows[dt.Rows.Count - 1][nStrategyIndex + 1] = string.Format("<b>{0}%</b>",
                        Math.Round(Strategy.allStrategies[nStrategyIndex].LAST_CHANGE,2));
                }
            }

            //Console.WriteLine(results[strategies.None].Keys.Count + " : " + Math.Round(Strategy.allStrategies[(int)strategies.None].LAST_CHANGE, 2));

            if (results[0].Count > 0)
                return convertDataTableToHTML("Today's performance", dt, results[0].Count > 0);
            else
                return string.Empty;
        }
        public static string makeHistoryTable()
        {
            DataTable dt = Strategy.makeStrategiesTable(false);
            DataRow dr = null;
            List<double> last = new List<double>();
            foreach (var item in Strategy.allStrategies)
                last.Add(Consts.MONEY_START_AMMOUNT);

            foreach (DateTime currDate in history.Keys)
            {
                dr = dt.NewRow();
                dt.Rows.Add(dr);
                dr[0] = currDate.ToShortDateString();
            }
            dr = dt.NewRow();
            dt.Rows.Add(dr);
            dr[0] = "<b>FINAL</b>";

            foreach (DataRow currDr in dt.Rows)
            {
                for (int nColIndex = 1; nColIndex < dt.Columns.Count; nColIndex++)
                {
                    currDr[nColIndex] = 0;
                }
            }


            int nRowIndex = 0;
            
            foreach (Dictionary<strategies, double> currHist in history.Values)
            {
                for (int nIndex = 0; nIndex < currHist.Count; nIndex++)
                {
                    double change = currHist[(strategies)nIndex];
                    
                    dt.Rows[nRowIndex][nIndex + 1] = string.Format("<font color='{0}'>{1}</font>",
                        change > last[nIndex] ? "green" : "red",
                        string.Format("{0:n0}", change));

                    last[nIndex] = change;
                }
                nRowIndex++;
            }

            for (int i = 0; i < last.Count; i++)
            {
                dt.Rows[dt.Rows.Count - 1][i + 1] = string.Format("<b><font color='{0}'>{1}$</font></b>",
                    last[i] > Consts.MONEY_START_AMMOUNT ? "green" : "red",
                    string.Format("{0:n0}", last[i]));
            }

            return convertDataTableToHTML("Performance history ", dt, true);
        }
        public static string convertDataTableToHTML(string title, DataTable dt, bool bContainsData)
        {
            StringBuilder sb = new StringBuilder(string.Format("<br><br><br><u><b>{0}:</b></u><br>", title));

            if (bContainsData)
            {
                sb.Append("<table>");

                //add header row
                sb.Append("<tr>");
                foreach (DataColumn currCol in dt.Columns)
                    sb.AppendFormat("<th>{0}</th>", currCol.ColumnName);
                sb.Append("</tr>");

                //add rows
                foreach (DataRow currRow in dt.Rows)
                {
                    sb.Append("<tr>");
                    for (int i = 0; i < dt.Columns.Count; i++)
                        sb.AppendFormat("<td>{0}</td>", currRow[i].ToString());
                    sb.Append("</tr>");
                }

                sb.Append("</table>");
            }


            return sb.ToString();
        }




        public static string style = @"<head>
<style>
table {
    font-family: 'Trebuchet MS', Arial, Helvetica, sans-serif;
    border-collapse: collapse;
    width: 100%;
}

td, th {
    border: 1px solid #ddd;
    padding: 8px;
}
tr:nth-child(even){background-color: #f2f2f2;}

tr:hover {background-color: #ddd;}

th {
padding-top: 12px;
    padding-bottom: 12px;
    text-align: left;
    background-color: #4CAF50;
    color: white;
}
</style>
</head>";
    }
}
