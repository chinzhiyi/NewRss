using HtmlAgilityPack;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using YahooFinanceApi;
using System.Numerics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace NewsRSSWithStockRecommender
{
    public partial class Form1 : Form
    {
        private Dictionary<int, List<int>> relatedArticlesMap = new Dictionary<int, List<int>>();

        private List<string> companyCodes = new List<string>(); // List to store company codes // Declare at the class level to be accessed in the event handler

        private Dictionary<int, List<int>> relatedCompanyMap = new Dictionary<int, List<int>>();
        public Form1()
        {
            InitializeComponent();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            transparentColor();
            this.datetime.Text = DateTime.Now.ToString();
            datetime.Font = new Font("Stencil", 12, FontStyle.Bold | FontStyle.Italic);

            //await WriteFile();
            getArticle();

            getCompany();

            // Extract keywords from company profiles
            ExtractCompanyKeywords();

            // Extract keywords from articles and compute similarities
            ComputeArticleToCompanySimilarities();


            // Load related articles data from file
            LoadRelatedArticlesFromFile();

            LoadRelatedCompanyFromFile();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnMin_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnMax_Click(object sender, EventArgs e)
        {
            switch (this.WindowState)
            {
                case FormWindowState.Normal:
                    this.WindowState = FormWindowState.Maximized;
                    datetime.Font = new Font("Stencil", 16, FontStyle.Bold | FontStyle.Italic);

                    break;

                case FormWindowState.Maximized:
                    this.WindowState = FormWindowState.Normal;
                    datetime.Font = new Font("Stencil", 12, FontStyle.Bold | FontStyle.Italic);
                    break;
            }

            // Update the positions of controls
            UpdateControlPositions();

        }

        private void UpdateControlPositions()
        {
            // Calculate the center position of the form
            int centerX = this.Width / 2;
            int centerY = this.Height / 2;

            // Calculate the new positions for controls
            int comboBoxX = centerX - comboBox1.Width / 2;
            int richTextBox1X = centerX - richTextBox1.Width / 2;

            // Make sure each control is anchored or positioned correctly
            comboBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            richTextBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            relatedList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            // Assuming chartPanel is your chart area, set it to anchor to the right side
            chartPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            // Position the Radio Buttons
            buyRB.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            sellRB.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            // Set the X position to be aligned with the chart panel
            int radioButtonX = chartPanel.Location.X; // Aligns with the left of the chart panel

            // Place buyRB above the chart panel
            int buyButtonY = chartPanel.Location.Y - buyRB.Height - 20; // 10px padding above chart panel
            buyRB.Location = new Point(radioButtonX, buyButtonY);

            // Place sellRB directly below buyRB
            int sellButtonY = buyButtonY + buyRB.Height + 5; // 5px padding between the buttons
            sellRB.Location = new Point(radioButtonX, sellButtonY);

            // Set the new positions for controls
            comboBox1.Location = new Point(comboBoxX, comboBox1.Location.Y);
            richTextBox1.Location = new Point(richTextBox1X, richTextBox1.Location.Y);
            relatedList.Location = new Point(centerX - relatedList.Width / 2, relatedList.Location.Y);
            //Set the new positions for pictureBox3
            pictureBox3.Location = new Point(comboBox1.Right, pictureBox3.Location.Y);
            int labelX = relatedList.Left; // Align with the left of relatedList
            int labelY = relatedList.Top - label2.Height - 10; // Place 10px above the relatedList
            label2.Location = new Point(labelX, labelY);
        }

        private void transparentColor()
        {
            pictureBox3.Parent = pictureBox5;

            pictureBox3.BackColor = Color.Transparent;

            label2.Parent = pictureBox5;

            label2.BackColor = Color.Transparent;

            pictureBox3.Parent = pictureBox5;

            pictureBox3.BackColor = Color.Transparent;

            datetime.Parent = panel2;

            datetime.BackColor = Color.Transparent;

            noCompanyLabel.Parent = pictureBox5;

            noCompanyLabel.BackColor = Color.Transparent;


        }


        private async void RadioButton_CheckedChanged(object sender, EventArgs e)
        {
            // Ensure that charts are only updated if there are company codes available
            if (companyCodes.Count > 0)
            {
                // Clear the existing charts in the panel
                chartPanel.Controls.Clear();

                // Create a StockFetcher instance to fetch stock data
                StockFetcher stockFetcher = new StockFetcher();
                List<Tuple<Chart, double, string>> chartList = new List<Tuple<Chart, double, string>>(); // Add string for Buy/Sell/None
                int chartIndex = 0;

                // Check if any of the indicators are selected
                bool isIndicatorSelected = RSIradioButton1.Checked || macdRadioBtn.Checked || SMArb.Checked || EMArb.Checked;

                // Check if Buy/Sell is selected
                bool isBuySellSelected = buyRB.Checked || sellRB.Checked;

                // Iterate through each company code to regenerate the charts
                foreach (string companyCode in companyCodes)
                {
                    // Fetch historical stock data asynchronously
                    var stockData = await stockFetcher.GetStockDataAsync(companyCode, 5); 

                    // Convert stockData to double if needed
                    var closePricesDouble = stockData.Select(data => (double)data.Close).ToList();
                    var dates = stockData.Select(data => data.Date).ToList();

                    string trimmedCompanyCode = companyCode.Replace(".KL", "");

                    // Find the corresponding company object
                    Company? company = companyList.FirstOrDefault(c => (c.CompanyCode?.Trim() + ".KL") == companyCode);

                    // Generate the chart
                    if (company == null) continue;
                    Chart chart = GenerateChart(company, stockData);

                    // Find the last stock price and the last trend value
                    double lastStockPrice = closePricesDouble.Last();
                    double[] trendLine = CalculateTrendLine(dates, closePricesDouble);
                    double lastTrendValue = trendLine.Last();

                    // Calculate the difference
                    double difference = lastTrendValue - lastStockPrice;

                    string signal = "None"; // Default if no Buy/Sell signal
                                            // Determine Buy/Sell signal
                    if (buyRB.Checked && lastTrendValue > lastStockPrice)
                    {
                        signal = "Buy";
                    }
                    else if (sellRB.Checked && lastTrendValue < lastStockPrice)
                    {
                        signal = "Sell";
                    }

                    // Add the chart to the list regardless of Buy/Sell selection
                    chartList.Add(new Tuple<Chart, double, string>(chart, difference, signal));
                }

                chartList.Sort((x, y) =>
                {
                    // Determine signal types
                    bool xIsSell = IsSellSignal(x.Item2);
                    bool yIsSell = IsSellSignal(y.Item2);
                    bool xIsBuy = IsBuySignal(x.Item2);
                    bool yIsBuy = IsBuySignal(y.Item2);

                    // Prioritize Sell signals first
                    if (xIsSell && !yIsSell)
                    {
                        return -1; // Sell signals come first
                    }
                    else if (!xIsSell && yIsSell)
                    {
                        return 1; // Buy signals come after Sell
                    }

                    // Prioritize Buy signals after Sell
                    if (xIsBuy && !yIsBuy)
                    {
                        return -1; // Buy signals come next
                    }
                    else if (!xIsBuy && yIsBuy)
                    {
                        return 1; // None comes after Buy
                    }

                    // If both are the same type (Sell, Buy, or None), sort by the difference in descending order
                    return y.Item2.CompareTo(x.Item2); // Descending order for all types
                });

                // Calculate number of rows and columns
                int numCharts = chartList.Count;
                int numColumns = 1; // Adjust number of columns as needed
                int numRows = (int)Math.Ceiling((double)numCharts / numColumns);

                // Calculate chart width and height
                int chartWidth = (chartPanel.Width / numColumns) - 20;
                int chartHeight = (int)(chartPanel.Height * 0.8);

                // Add the sorted charts to the panel
                foreach (var chartTuple in chartList)
                {
                    Chart chart = chartTuple.Item1;
                    string signal = chartTuple.Item3; // Retrieve the signal (Buy/Sell/None)

                    // Set chart size
                    chart.Width = chartWidth;
                    chart.Height = chartHeight;

                    // Remove DockStyle.Fill if it's set
                    chart.Dock = DockStyle.None; // This is important to allow manual positioning

                    // Set the chart's location dynamically
                    int row = chartIndex / numColumns;
                    int col = chartIndex % numColumns;
                    // Calculate the Y position based on the row index
                    int yPos = row * (chart.Height + 10);

                    chart.Location = new Point(col * (chart.Width + 10), yPos);
                    chartPanel.Controls.Add(chart);

                    // Optionally, highlight charts with Buy/Sell signals (e.g., color coding)
                    if (signal == "Buy")
                    {
                        chart.BackColor = Color.LightGreen; // Example for Buy signal
                    }
                    else if (signal == "Sell")
                    {
                        chart.BackColor = Color.LightCoral; // Example for Sell signal
                    }

                    chartIndex++;
                }
            }
        }

        // Helper method to determine if the signal is a "Sell"
        private bool IsSellSignal(double difference)
        {
            return difference < 0; // Sell if the trendline is above the close price
        }

        // Helper method to determine if the signal is a "Buy"
        private bool IsBuySignal(double difference)
        {
            return difference > 0; // Buy if the trendline is below the close price
        }

        private async void searchBtn(object sender, EventArgs e)
        {
            // Get the selected article title from the ComboBox
            string? selectedTitle = comboBox1.SelectedItem?.ToString();

            // Find the index of the selected article
            int selectedArticleIndex = GetArticleIndex(selectedTitle) + 1;

            // Find the corresponding article based on the selected title
            Article? selectedArticle = articles.FirstOrDefault(article => article.Title == selectedTitle);

            // Check if the selected article exists
            if (selectedArticle != null)
            {
                // Display the content of the selected article in richTextBox1
                richTextBox1.Text = selectedArticle.Content;

                // Check if the selected article exists and has related articles
                if (selectedArticleIndex != -1 && relatedArticlesMap.ContainsKey(selectedArticleIndex))
                {
                    // Get the related article indices for the selected article
                    List<int> relatedIndices = relatedArticlesMap[selectedArticleIndex];

                    // Clear the items in the relatedList
                    relatedList.Items.Clear();

                    // Add the related articles' titles to the relatedList
                    foreach (int index in relatedIndices)
                    {
                        // Get the title of the related article using its index
                        string title = GetArticleTitle(index - 1);

                        // Add the title to the relatedList
                        relatedList.Items.Add(title);
                    }
                }

                // Check if the selected article exists and has related companies
                if (selectedArticleIndex != -1 && relatedCompanyMap.ContainsKey(selectedArticleIndex))
                {
                    // Get the related company indices for the selected article
                    List<int> relatedCompanyIndices = relatedCompanyMap[selectedArticleIndex];

                    // Clear any existing charts in the panel
                    chartPanel.Controls.Clear();

                    if (relatedCompanyIndices.Count > 0)
                    {
                        noCompanyLabel.Visible = false;

                        StockFetcher stockFetcher = new StockFetcher();
                        int chartIndex = 0;

                        // Clear the previous company codes and prepare for new ones
                        companyCodes.Clear();

                        // List to store charts and their differences
                        List<Tuple<Chart, double>> chartList = new List<Tuple<Chart, double>>();

                        foreach (int companyIndex in relatedCompanyIndices)
                        {
                            // Ensure companyIndex is within bounds
                            if (companyIndex > 0 && companyIndex <= companyList.Count)
                            {
                                Company company = companyList[companyIndex - 1];

                                // Append ".KL" to the company code
                                string fullCompanyCode = company.CompanyCode?.Trim() + ".KL";

                                // Store the full company code in the list
                                companyCodes.Add(fullCompanyCode);

                                // Fetch historical stock data asynchronously
                                List<StockData> stockData = await stockFetcher.GetStockDataAsync(fullCompanyCode, 5);

                                // Convert stockData to double if needed
                                var closePricesDouble = stockData.Select(data => (double)data.Close).ToList();
                                var dates = stockData.Select(data => data.Date).ToList();

                                // Generate the chart
                                Chart chart = GenerateChart(company, stockData);

                                // Find the last stock price and the last trend value
                                double lastStockPrice = closePricesDouble.Last();
                                double[] trendLine = CalculateTrendLine(dates, closePricesDouble);
                                double lastTrendValue = trendLine.Last();

                                // Calculate the difference
                                double difference = Math.Abs(lastStockPrice - lastTrendValue);

                                // Add chart and difference to the list
                                chartList.Add(new Tuple<Chart, double>(chart, difference));
                            }
                        }

                        // Sort the list by difference in descending order
                        chartList.Sort((x, y) => y.Item2.CompareTo(x.Item2));

                        // Calculate number of rows and columns
                        int numCharts = chartList.Count;
                        int numColumns = 1; // Adjust number of columns as needed
                        int numRows = (int)Math.Ceiling((double)numCharts / numColumns);

                        // Calculate chart width and height
                        int chartWidth = (chartPanel.Width / numColumns) - 20;
                        int chartHeight = (int)(chartPanel.Height * 0.8);

                        // Add the sorted charts to the panel
                        foreach (var chartTuple in chartList)
                        {
                            Chart chart = chartTuple.Item1;

                            // Set chart size
                            chart.Width = chartWidth;
                            chart.Height = chartHeight;

                            // Remove DockStyle.Fill if it's set
                            chart.Dock = DockStyle.None; // This is important to allow manual positioning

                            // Set the chart's location dynamically
                            int row = chartIndex / numColumns;
                            int col = chartIndex % numColumns;
                            // Calculate the Y position based on the row index
                            int yPos = row * (chart.Height + 10);

                            chart.Location = new Point(col * (chart.Width + 10), yPos);
                            chartPanel.Controls.Add(chart);
                            
                            chartIndex++;
                        }

                        // Show the indicator panel after displaying the charts
                        indicatorPanel.Visible = true;

                        //show the buy and sell radio button displaying the charts
                        buyRB.Visible = true;
                        sellRB.Visible = true;
                    }
                    else
                    {
                        noCompanyLabel.Visible = true;
                    }
                }
                else
                {
                    noCompanyLabel.Visible = true;
                    chartPanel.Controls.Clear();
                }
            }
            else
            {
                // Clear the content of the richTextBox1 if no article is selected
                richTextBox1.Clear();
                relatedList.Items.Clear();
                chartPanel.Controls.Clear();
                noCompanyLabel.Visible = true;
                indicatorPanel.Visible = false;
            }
        }





        // Generate Chart
        private Chart GenerateChart(Company company, List<StockData> stockData)
        {
            // Calculate the min and max closing prices for y-axis range
            var closePrices = stockData.Select(data => data.Close).ToList();
            double minClose = closePrices.Min();
            double maxClose = closePrices.Max();

            // Dynamic padding based on the range of prices
            double range = maxClose - minClose;
            double yAxisPadding = range * 0.1; // 10% padding

            // Ensure minimum value is reasonable and avoid negative values
            double minRounded = Math.Floor((minClose - yAxisPadding) * 1000) / 1000;
            if (minRounded < 0) minRounded = 0;

            double maxRounded = Math.Ceiling((maxClose + yAxisPadding) * 1000) / 1000;
            // Create a new chart
            Chart chart = new Chart
            {

                Dock = DockStyle.Fill  // Make sure it fits inside the panel
            };

            // Determine the height of the MainArea based on the selected radio button
            ElementPosition mainAreaPosition;
            if (RSIradioButton1.Checked)
            {
                mainAreaPosition = new ElementPosition(0, 20, 100, 50); // 50% height if RSI is shown
            }
            else if (macdRadioBtn.Checked)
            {
                mainAreaPosition = new ElementPosition(0, 20, 100, 50); // 50% height if MACD is shown
            }
            else if (remove_rb.Checked || SMArb.Checked || EMArb.Checked)
            {
                mainAreaPosition = new ElementPosition(0, 20, 100, 70); // Restore to 70% height if RSI is removed
            }
            else
            {
                mainAreaPosition = new ElementPosition(0, 20, 100, 70); // Default to 70% if nothing else is selected
            }

            ChartArea chartArea = new ChartArea("MainArea")
            {
                Position = mainAreaPosition,
                //Position = new ElementPosition(0, 20, 100, 50), // Take up 75% of the height
                AxisX = new Axis
                {
                    MajorGrid = { LineColor = Color.LightGray, LineDashStyle = ChartDashStyle.Dot },
                    LabelStyle = { Format = "MM/yyyy", Angle = -45 },
                    IntervalType = DateTimeIntervalType.Years,
                    Interval = 1,
                    IsMarginVisible = false
                },
                AxisY = new Axis
                {
                    MajorGrid = { LineColor = Color.LightGray, LineDashStyle = ChartDashStyle.Dot },

                    //y-axis mininum
                    Minimum = minRounded,
                    Maximum = maxRounded,

                    //Set the auto fit to none
                    LabelAutoFitStyle = LabelAutoFitStyles.None,

                    //change font size
                    LabelStyle = new LabelStyle { Font = new Font("Arial", 8) },

                    Interval = Math.Round((maxClose - minClose) / 10, 3)
                }
            };


            chart.ChartAreas.Add(chartArea);

            Series series = new Series
            {
                Name = "StockPrice",
                Color = Color.Red,
                ChartType = SeriesChartType.Line,
                XValueType = ChartValueType.Date,
                BorderWidth = 1 // Set the line width to 1 for a thinner line
            };

            chart.Series.Add(series);



            // Store dates and closing prices for trend line calculation
            List<DateTime> dates = new List<DateTime>();
            List<double> prices = new List<double>();


            foreach (var data in stockData)
            {
                series.Points.AddXY(data.Date, data.Close);
                dates.Add(data.Date);
                prices.Add(data.Close);
            }

            // Calculate trend line
            double[] trendLine = CalculateTrendLine(dates, prices);

            Series trendSeries = new Series
            {
                Name = "TrendLine",
                Color = Color.FromArgb(60, Color.Blue),
                ChartType = SeriesChartType.Line,
                XValueType = ChartValueType.Date,
                BorderDashStyle = ChartDashStyle.Dash,
                BorderWidth = 1,

            };

            for (int i = 0; i < dates.Count; i++)
            {
                trendSeries.Points.AddXY(dates[i], trendLine[i]);
            }

            chart.Series.Add(trendSeries);

            // Conditionally calculate and add the SMA (50) if SMArb is checked
            if (SMArb.Checked)
            {
                List<double> smaValues = CalculateSMA(prices, 50);

                Series smaSeries = new Series
                {
                    Name = "SMA50",
                    Color = Color.Blue, // You can choose any color you prefer
                    ChartType = SeriesChartType.Line,
                    XValueType = ChartValueType.Date,
                    BorderWidth = 1
                };

                for (int i = 0; i < dates.Count; i++)
                {
                    smaSeries.Points.AddXY(dates[i], smaValues[i]);
                }

                chart.Series.Add(smaSeries);
            }

            // Conditionally calculate and add the EMA (50) if EMArb is checked
            if (EMArb.Checked)
            {
                List<double> emaValues = CalculateEMAs(prices, 50);

                Series emaSeries = new Series
                {
                    Name = "EMA50",
                    Color = Color.OliveDrab, // You can choose any color you prefer
                    ChartType = SeriesChartType.Line,
                    XValueType = ChartValueType.Date,
                    BorderWidth = 1
                };

                for (int i = 0; i < dates.Count; i++)
                {
                    emaSeries.Points.AddXY(dates[i], emaValues[i]);
                }

                chart.Series.Add(emaSeries);
            }

            // Conditionally add RSI chart area and series if the RSI radio button is checked
            if (RSIradioButton1.Checked)
            {
                // RSI ChartArea below the Stock Prices
                ChartArea chartAreaRSI = new ChartArea("RSIArea")
                {
                    Position = new ElementPosition(0, 70, 100, 25), // Positioned below the price chart
                    AlignWithChartArea = "MainArea",
                    BorderColor = Color.Gray,
                    AxisX = new Axis
                    {
                        MajorGrid = { LineColor = Color.LightGray, LineDashStyle = ChartDashStyle.Solid },
                        LabelStyle = { Format = "MM/yyyy", Angle = -45 },
                        IntervalType = DateTimeIntervalType.Months,
                        Interval = 1,
                        IsMarginVisible = false
                    },
                    AxisY = new Axis
                    {
                        MajorGrid = { LineColor = Color.LightGray, LineDashStyle = ChartDashStyle.Solid },
                        Minimum = 0,
                        Maximum = 100,
                        Interval = 20,
                        IsStartedFromZero = true,
                        CustomLabels =
                {
                    new CustomLabel(18, 22, "20", 0, LabelMarkStyle.LineSideMark),
                    new CustomLabel(78, 82, "80", 0, LabelMarkStyle.LineSideMark)
                }
                    }
                };

                chart.ChartAreas.Add(chartAreaRSI);

                // RSI Series
                double[] rsiValues = CalculateRSI(stockData.Select(s => (double)s.Close).ToList(), 14);
                Series rsiSeries = new Series
                {
                    Name = "RSI",
                    Color = Color.Green,
                    ChartType = SeriesChartType.Line,
                    XValueType = ChartValueType.Date,
                    ChartArea = "RSIArea"
                };

                for (int i = 0; i < stockData.Count; i++)
                {
                    rsiSeries.Points.AddXY(stockData[i].Date, rsiValues[i]);
                }

                chart.Series.Add(rsiSeries);

                // Link the X axes of the two ChartAreas
                chartAreaRSI.AxisX = chartArea.AxisX;
            }

            // Conditionally add MACD chart area and series if the macdRadioBtn is checked
            if (macdRadioBtn.Checked)
            {
                // MACD ChartArea below the Stock Prices
                ChartArea chartAreaMACD = new ChartArea("MACDArea")
                {
                    Position = new ElementPosition(0, 70, 100, 25), // Positioned below the price chart
                    AlignWithChartArea = "MainArea",
                    BorderColor = Color.Gray,
                    AxisX = new Axis
                    {
                        MajorGrid = { LineColor = Color.LightGray, LineDashStyle = ChartDashStyle.Solid },
                        LabelStyle = { Format = "MM/yyyy", Angle = -45 },
                        IntervalType = DateTimeIntervalType.Months,
                        Interval = 1,
                        IsMarginVisible = false
                    },
                    AxisY = new Axis
                    {
                        MajorGrid = { LineColor = Color.LightGray, LineDashStyle = ChartDashStyle.Solid },
                        Minimum = -2,   // Set minimum to -2
                        Maximum = 2,    // Set maximum to 2
                        Interval = 1, // Set interval as needed
                        IsStartedFromZero = true,
                        LabelAutoFitStyle = LabelAutoFitStyles.None // Disable auto-fitting
                    }
                };

                chart.ChartAreas.Add(chartAreaMACD);

                // MACD Series
                double[] macdLine = CalculateMACDLine(stockData.Select(s => (double)s.Close).ToList(), 12, 26);
                Series macdSeries = new Series
                {
                    Name = "MACD",
                    Color = Color.Blue,
                    ChartType = SeriesChartType.Line,
                    XValueType = ChartValueType.Date,
                    ChartArea = "MACDArea",
                    BorderWidth = 1
                };

                for (int i = 0; i < stockData.Count; i++)
                {
                    macdSeries.Points.AddXY(stockData[i].Date, macdLine[i]);
                }

                chart.Series.Add(macdSeries);

                // Signal Line Series
                double[] signalLine = CalculateSignalLine(macdLine, 9);
                Series signalSeries = new Series
                {
                    Name = "SignalLine",
                    Color = Color.Orange,
                    ChartType = SeriesChartType.Line,
                    XValueType = ChartValueType.Date,
                    ChartArea = "MACDArea",
                    BorderWidth = 1
                };

                for (int i = 0; i < stockData.Count; i++)
                {
                    signalSeries.Points.AddXY(stockData[i].Date, signalLine[i]);
                }

                chart.Series.Add(signalSeries);

                // Link the X axes of the two ChartAreas
                chartAreaMACD.AxisX = chartArea.AxisX;
            }

            // Find the intersection point with the stock price line and calculate the difference
            double lastStockPrice = prices.Last();
            double lastTrendValue = trendLine.Last();
            double difference = lastTrendValue - lastStockPrice;

            // Display Buy or Sell annotation based on radio button and price vs trendline
            if (buyRB.Checked && lastTrendValue > lastStockPrice)
            {
                // Add "Buy" annotation if Buy condition is true
                TextAnnotation annotationBuy = new TextAnnotation
                {
                    Text = "Buy",
                    ForeColor = Color.Green,
                    Font = new Font("Arial", 10, FontStyle.Bold),
                    AnchorDataPoint = series.Points.Last(),
                    AnchorX = series.Points.Last().XValue,
                    AnchorY = series.Points.Last().YValues[0],
                    Alignment = ContentAlignment.MiddleCenter
                };
                chart.Annotations.Add(annotationBuy);
            }
            else if (sellRB.Checked && lastTrendValue < lastStockPrice)
            {
                // Add "Sell" annotation if Sell condition is true
                TextAnnotation annotationSell = new TextAnnotation
                {
                    Text = "Sell",
                    ForeColor = Color.Red,
                    Font = new Font("Arial", 10, FontStyle.Bold),
                    AnchorDataPoint = series.Points.Last(),
                    AnchorX = series.Points.Last().XValue,
                    AnchorY = series.Points.Last().YValues[0],
                    Alignment = ContentAlignment.MiddleCenter
                };
                chart.Annotations.Add(annotationSell);
            }

            // Display the percentage difference on the chart
            double percentageDifference = Math.Abs(difference) * 100;

            // Display the difference on the chart
            TextAnnotation annotation = new TextAnnotation
            {
                Text = $"Difference: {percentageDifference:F2}%",
                ForeColor = Color.Black,
                Font = new Font("Arial", 8),
                AnchorDataPoint = series.Points.Last(),
                AnchorX = series.Points.Last().XValue,
                AnchorY = series.Points.Last().YValues[0],
                Alignment = ContentAlignment.MiddleRight,
                AxisX = chartArea.AxisX,
                AxisY = chartArea.AxisY,
            };
            chart.Annotations.Add(annotation);

            // Additional formatting for a cleaner look
            chart.Legends.Clear();
            Legend legend = new Legend
            {
                Docking = Docking.Top, // Change the docking position to Bottom
                Alignment = StringAlignment.Center,
                LegendStyle = LegendStyle.Table, // Use Table style to manage multiple items
                DockedToChartArea = "MainArea", // Ensure the legend is docked to the MainArea
                Position = new ElementPosition(0, 7, 100, 12), // Adjust the Position to create more space
                IsDockedInsideChartArea = false, // Ensure legend is outside of the chart area
                TableStyle = LegendTableStyle.Wide // Use Wide table style to spread items horizontally
            };

            // Optionally, set the number of rows or columns to accommodate more items
            legend.MaximumAutoSize = 20; // Allow more space for the legend
            legend.IsTextAutoFit = true; // Ensure text is resized to fit

            chart.Legends.Add(legend);

            chart.Titles.Clear();
            chart.Titles.Add(company.Name);
            chart.Titles[0].Font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold);
            chart.Titles[0].Docking = Docking.Top; // Dock the title at the top
            chart.Titles[0].DockingOffset = 0;  // Move title upwards by a small offset if needed

            chart.Invalidate();
            return chart;
        }

        private List<double> CalculateSMA(List<double> prices, int period)
        {
            List<double> sma = new List<double>();

            for (int i = 0; i < prices.Count; i++)
            {
                if (i >= period - 1)
                {
                    double sum = 0;
                    for (int j = 0; j < period; j++)
                    {
                        sum += prices[i - j];
                    }
                    sma.Add(sum / period);
                }
                else
                {
                    sma.Add(double.NaN); // Adding NaN for the initial values where SMA cannot be calculated
                }
            }

            return sma;
        }
        
        private double[] CalculateMACDLine(List<double> closePrices, int shortPeriod, int longPeriod)
        {
            double[] shortEMA = CalculateEMA(closePrices, shortPeriod);
            double[] longEMA = CalculateEMA(closePrices, longPeriod);

            double[] macdLine = new double[closePrices.Count];
            for (int i = 0; i < closePrices.Count; i++)
            {
                macdLine[i] = shortEMA[i] - longEMA[i];
            }
            return macdLine;
        }

        private double[] CalculateSignalLine(double[] macdLine, int signalPeriod)
        {
            return CalculateEMA(macdLine.ToList(), signalPeriod);
        }

        private List<double> CalculateEMAs(List<double> prices, int period)
        {
            List<double> ema = new List<double>();

            double multiplier = 2.0 / (period + 1);
            double prevEma = prices.Take(period).Average(); // Initial EMA based on SMA of the first 'period' prices
            ema.Add(prevEma);

            for (int i = period; i < prices.Count; i++)
            {
                double currentEma = ((prices[i] - prevEma) * multiplier) + prevEma;
                ema.Add(currentEma);
                prevEma = currentEma;
            }

            // Prepend NaNs for the initial values where EMA cannot be calculated
            for (int i = 0; i < period - 1; i++)
            {
                ema.Insert(0, double.NaN);
            }

            return ema;
        }

        private double[] CalculateEMA(List<double> prices, int period)
        {
            double[] ema = new double[prices.Count];
            double multiplier = 2.0 / (period + 1);

            // First EMA value is simply the first price
            ema[0] = prices[0];

            // Calculate the rest of the EMA values
            for (int i = 1; i < prices.Count; i++)
            {
                ema[i] = ((prices[i] - ema[i - 1]) * multiplier) + ema[i - 1];
            }
            return ema;
        }

        // RSI Calculation
        private double[] CalculateRSI(List<double> closePrices, int period)
        {
            double[] rsi = new double[closePrices.Count];
            double[] gains = new double[closePrices.Count];
            double[] losses = new double[closePrices.Count];

            for (int i = 1; i < closePrices.Count; i++)
            {
                double change = closePrices[i] - closePrices[i - 1];
                gains[i] = Math.Max(0, change);
                losses[i] = Math.Max(0, -change);
            }

            double avgGain = gains.Take(period).Average();
            double avgLoss = losses.Take(period).Average();

            rsi[period] = avgLoss == 0 ? 100 : 100 - (100 / (1 + avgGain / avgLoss));

            for (int i = period + 1; i < closePrices.Count; i++)
            {
                avgGain = (avgGain * (period - 1) + gains[i]) / period;
                avgLoss = (avgLoss * (period - 1) + losses[i]) / period;

                rsi[i] = avgLoss == 0 ? 100 : 100 - (100 / (1 + avgGain / avgLoss));
            }

            return rsi;
        }

        private double[] CalculateTrendLine(List<DateTime> dates, List<double> prices)
        {
            int n = dates.Count;
            double[] trendLine = new double[n];

            // Convert dates to numeric values
            double[] xValues = dates.Select(date => date.ToOADate()).ToArray();
            double[] yValues = prices.ToArray();

            double sumX = xValues.Sum();
            double sumY = yValues.Sum();
            double sumXY = xValues.Zip(yValues, (x, y) => x * y).Sum();
            double sumX2 = xValues.Select(x => x * x).Sum();

            // Calculate slope (m) and intercept (b) for y = mx + b
            double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            double intercept = (sumY - slope * sumX) / n;

            // Calculate trend line values
            for (int i = 0; i < n; i++)
            {
                trendLine[i] = slope * xValues[i] + intercept;
            }

            return trendLine;
        }


        public class StockFetcher
        {
            private string csvFolderPath = @"C:\Users\Zhiyi\Desktop\FYP1\NewsRSSWithStockRecommender\NewsRSSWithStockRecommender\bin\Debug\net8.0-windows\hist-dataset"; // Update this with the actual path to your CSV files

            // Load stock data from the local CSV file based on the symbol (e.g., "0001.KL")
            public async Task<List<StockData>> GetStockDataAsync(string symbol, int years)
            {
                // Build the file path based on the symbol (e.g., "0001.KL_historical_data_weekly.csv")
                string fileName = $"{symbol}_historical_data_weekly.csv";
                string filePath = Path.Combine(csvFolderPath, fileName);

                // Load data from the CSV file
                return await Task.Run(() => LoadCsvData(filePath));
            }

            private List<StockData> LoadCsvData(string filePath)
            {
                List<StockData> stockDataList = new List<StockData>();

                if (!System.IO.File.Exists(filePath))
                {
                    Console.WriteLine($"File not found: {filePath}");
                    return stockDataList; // Return an empty list if the file is not found
                }

                string[] csvLines = System.IO.File.ReadAllLines(filePath);

                if (csvLines.Length > 1)
                {
                    for (int i = 1; i < csvLines.Length; i++) // Start from 1 to skip the header
                    {
                        string[] data = csvLines[i].Split(',');

                        // Ensure the date format is valid and parse it
                        DateTime dateValue;
                        if (!DateTime.TryParse(data[0], out dateValue))
                        {
                            Console.WriteLine($"Invalid date format: {data[0]}");
                            continue; // Skip rows with invalid dates
                        }

                        // Filter to only include dates from 2020 to 2024
                        if (dateValue.Year >= 2020 && dateValue.Year <= 2024)
                        {
                            StockData stockData = new StockData
                            {
                                Date = dateValue,
                                Open = float.Parse(data[1]),
                                High = float.Parse(data[2]),
                                Low = float.Parse(data[3]),
                                Close = float.Parse(data[4]),
                                Volume = long.Parse(data[5])
                            };

                            stockDataList.Add(stockData);
                        }
                    }
                }

                return stockDataList;
            }
        }


        private int GetArticleIndex(string? title)
        {
            // Search for the article in your articles list and return its index
            if (title != null)
            {
                for (int i = 0; i < articles.Count; i++)
                {
                    if (articles[i].Title == title)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private string GetArticleTitle(int index)
        {
            // Retrieve the title of the article with the specified index
            if (index >= 0 && index < articles.Count)
            {
                return articles[index].Title!;
            }
            return string.Empty;
        }

        // Method to load related company from file
        private void LoadRelatedCompanyFromFile()
        {
            try
            {
                string filePath = "MatchingCompany.txt";
                relatedCompanyMap = new Dictionary<int, List<int>>();

                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath);

                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length > 1)
                        {
                            int articleIndex = int.Parse(parts[0].Trim());
                            string relatedCompaniesStr = parts[1].Trim();

                            List<int> relatedIndices = new List<int>();

                            if (!string.IsNullOrEmpty(relatedCompaniesStr))
                            {
                                string[] relatedIndicesStr = relatedCompaniesStr.Split(',');

                                foreach (string indexStr in relatedIndicesStr)
                                {
                                    if (int.TryParse(indexStr, out int index))
                                    {
                                        relatedIndices.Add(index);
                                    }
                                }
                            }

                            relatedCompanyMap.Add(articleIndex, relatedIndices);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading related companies: " + ex.Message);
            }
        }

        // Method to load related articles from file
        private void LoadRelatedArticlesFromFile()
        {
            try
            {
                string filePath = "MatchingArticles.txt";
                relatedArticlesMap = new Dictionary<int, List<int>>();
                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath);

                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length > 1)
                        {
                            int articleIndex = int.Parse(parts[0].Trim());
                            string[] relatedIndicesStr = parts[1].Trim().Split(',');
                            List<int> relatedIndices = new List<int>();
                            foreach (string indexStr in relatedIndicesStr)
                            {
                                if (int.TryParse(indexStr, out int index))
                                {
                                    relatedIndices.Add(index);
                                }
                            }
                            relatedArticlesMap.Add(articleIndex, relatedIndices);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading related articles: " + ex.Message);
            }
        }


        //Write the program to set a timmer to update the news content every 1 hour
        private async void timer1_Tick(object? sender, EventArgs e)
        {
            await WriteFile();

            LoadRelatedArticlesFromFile();
        }

        public async Task WriteFile()
        {

            List<string> existingTitles = ReadExistingTitles(@"NewsContent.txt");

            Random random = new Random();

            // Create web client.
            var client = new HttpClient();

            // Download string.
            string value = await client.GetStringAsync("https://theedgemalaysia.com/");


            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(value);

            var items = doc.DocumentNode.SelectNodes("//script[@type='application/json']")
                .Select(p => p.InnerText)
                .ToList();

            if (items.Count > 0)
            {
                var result = JsonSerializer.Deserialize<Prop>(items[0]);
                var itemList = result?.props?.pageProps?.homeData;
                if (itemList?.Count > 0)
                {
                    foreach (var data in itemList)
                    {
                        var title = data.title;
                        var alias = data.alias;
                        var updated = data.updated;

                        var articleDate = DateTime.UnixEpoch.AddMilliseconds(updated).ToLocalTime().ToString("dddd, dd MMM yyyy");

                        string paragraphs = "";

                        string valueBrows = await client.GetStringAsync("https://theedgemalaysia.com/" + alias);

                        doc.LoadHtml(valueBrows);
                        var docNode = doc.DocumentNode.SelectNodes("//div[@class='newsTextDataWrapInner']//p");
                        if (docNode != null)
                        {
                            foreach (HtmlNode pg in docNode)
                            {
                                if (!pg.InnerHtml.Contains("<em>"))
                                {
                                    var content = HttpUtility.HtmlDecode(pg.InnerText.Trim());

                                    content = Regex.Replace(content, @"<br\s*/?>|\s+", " ");

                                    paragraphs += content + " ";
                                }

                            }

                            var infoNews = title + '■' + paragraphs + '■' + articleDate;


                            // Check if the title already exists in the list of existing titles
                            if (title != null && !existingTitles.Contains(title))
                            {
                                // Append the new article information if the title is not found
                                await File.AppendAllTextAsync(@"NewsContent.txt", infoNews + Environment.NewLine);

                                existingTitles.Add(title);
                            }
                        }

                        // Randomly shuffle the order of reading articles
                        var randomDelay = random.Next(7000); // Generate a random delay between 0 and 7 seconds
                        await Task.Delay(randomDelay);
                        //Thread.Sleep(randomDelay);





                    }
                    // Get the list of articles after updating the file
                }
                getArticle();
            }
        }


        public void getArticle()
        {
            articles.Clear();
            // Define the path to the file to be read
            string filePath = "NewsContent.txt";

            // Open the file for reading
            using (StreamReader reader = new StreamReader(filePath))
            {
                string? line;
                // Read each line until the end of the file
                while ((line = reader.ReadLine()) != null)
                {
                    // Split the line using the special character "■"
                    string[] parts = line.Split('■');

                    // Extract Title, Content, and Date from the split parts
                    string title = parts[0];
                    string content = parts[1].ToLower();
                    string date = parts[2];

                    // Create an Article object and add it to the list
                    articles.Add(new Article { Title = title, Content = content, Date = date });
                }
            }

            // Clear existing items in the combo box
            comboBox1.Items.Clear();
            comboBox1.Text = "Select News";

            // Add titles to the combo box
            foreach (var article in articles)
            {
                comboBox1.Items.Add(article.Title ?? "Unknown Title");
            }

            startRake();
        }

        //get the company name and description from text file and add into list
        public void getCompany()
        {
            companyList.Clear();
            // Define the path to the file to be read
            string filePath = "CompanyProfile.txt";

            // Open the file for reading
            using (StreamReader reader = new StreamReader(filePath))
            {
                string? line;
                // Read each line until the end of the file
                while ((line = reader.ReadLine()) != null)
                {
                    // Split the line using the special character "¤"
                    string[] parts = line.Split('¤');

                    // Extract Name and Description from the split parts
                    string name = parts[0];
                    string description = parts[1];
                    string companyCode = parts[2];

                    // Create a Company object and add it to the list
                    companyList.Add(new Company { Name = name, Description = description, CompanyCode = companyCode });
                }
            }
        }

        private List<NameScoreModel> ExtractKeywordsUsingRake(string content, List<string> stopwordList)
        {
            List<string> sList = new List<string>();
            List<string> dictionaryList = new List<string>();
            List<List<int>> matrixList = new List<List<int>>();
            List<List<string>> keywordCandidateList = new List<List<string>>();
            List<NameScoreModel> scoreList = new List<NameScoreModel>();

            string s = content.Replace(",", " ,")
                              .Replace(".", " .")
                              .Replace("(", "( ")
                              .Replace(")", " )")
                              .Replace("\"", "\" ")
                              .Replace("\"", " \"")
                              .Replace("“", "“ ")
                              .Replace(":", " :");

            sList = s.Split(' ').ToList();
            removeStopword(ref sList, stopwordList);

            findDistinctWord(ref dictionaryList, sList);
            createMatrix(ref matrixList, dictionaryList.Count);
            listOutKeywordCandidate(dictionaryList, sList, ref keywordCandidateList, ref matrixList);
            computeScore(matrixList, dictionaryList, ref scoreList);

            return scoreList;
        }

        private void ExtractCompanyKeywords()
        {
            // Read stopword list
            List<string> stopwordList = File.ReadAllLines("SmartStoplist.txt").ToList();

            foreach (var company in companyList)
            {
                var keywords = ExtractKeywordsUsingRake(company.Description ?? string.Empty, stopwordList);
                company.Keywords = keywords;
            }
        }

        private void ComputeArticleToCompanySimilarities()
        {
            List<string> stopwordList = File.ReadAllLines("SmartStoplist.txt").ToList();

            // Dictionary to store results for each article
            var articleResults = new Dictionary<int, List<int>>();

            foreach (var article in articles)
            {
                // Extract keywords with scores > 2 from the article
                var articleKeywords = ExtractKeywordsUsingRake(article.Content ?? string.Empty, stopwordList)
                    .Where(k => k.Score > 2)
                    .ToList();
                var articleKeywordDict = articleKeywords.ToDictionary(k => k.Name, k => k.Score);

                var articleResultsForCurrentArticle = new List<CompanySimilarity>();

                for (int companyIndex = 0; companyIndex < companyList.Count; companyIndex++)
                {
                    var company = companyList[companyIndex];

                    // Extract keywords with scores > 2 from the company profile
                    var companyKeywordDict = company.Keywords
                        .Where(k => k.Score > 2)
                        .ToDictionary(k => k.Name, k => k.Score);

                    // Get the list of keywords from the article
                    var allKeywords = articleKeywordDict.Keys.ToList();

                    // Create vectors for cosine similarity calculation
                    var articleVector = new List<double>();
                    var companyVector = new List<double>();

                    foreach (var keyword in allKeywords)
                    {
                        articleVector.Add(articleKeywordDict.ContainsKey(keyword) ? (double)articleKeywordDict[keyword] : 0);
                        companyVector.Add(companyKeywordDict.ContainsKey(keyword) ? (double)companyKeywordDict[keyword] : 0);
                    }

                    var similarity = GetCosineSimilarity(articleVector, companyVector);

                    if (similarity != 0.00)
                    {
                        articleResultsForCurrentArticle.Add(new CompanySimilarity
                        {
                            CompanyIndex = companyIndex + 1,
                            Similarity = similarity
                        });
                    }

                }

                // Sort the results for the current article by similarity
                articleResultsForCurrentArticle.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));

                // Extract only the company indices from the sorted results
                var sortedCompanyIndices = articleResultsForCurrentArticle.Select(cs => cs.CompanyIndex).ToList();

                // Add the sorted company indices to the dictionary
                articleResults.Add(articles.IndexOf(article) + 1, sortedCompanyIndices);
            }

            // Count the number of articles in the list
            int numberOfArticles = articles.Count;

            // Calculate the top articles
            double topArticles = Math.Log(numberOfArticles);
            int topMatchArticles = Convert.ToInt32(topArticles);

            StringBuilder matchingArticles = new StringBuilder();

            int count = 0; // Counter to track the number of articles processed

            foreach (var kvp in articleResults)
            {
                // Add the article pair to the matching articles string
                matchingArticles.Append($"{kvp.Key} = ");
                if (kvp.Value.Count > 0)
                {
                    matchingArticles.Append(string.Join(",", kvp.Value.Take(topMatchArticles)));
                }

                matchingArticles.AppendLine();

                count++;
                if (count >= topMatchArticles)
                {
                    // Reset the counter when the desired number of articles is reached
                    count = 0;
                }
            }

            // Write the matching articles to the file
            File.WriteAllText(@"MatchingCompany.txt", matchingArticles.ToString());
        }


        private void startRake()
        {
            string s;
            int index = 0;
            List<string> sList = new List<string>();
            List<string> dictionaryList = new List<string>();
            List<string> stopwordList = new List<string>();
            List<List<int>> matrixList = new List<List<int>>();
            List<List<string>> keywordCandidateList = new List<List<string>>();
            List<NameScoreModel> scoreList = new List<NameScoreModel>();
            // List<NameScoreModel> baseListA = new List<NameScoreModel>();
            //  List<NameScoreModel> compareList = new List<NameScoreModel>();
            List<List<NameScoreModel>> nameScoreList = new List<List<NameScoreModel>>();

            stopwordList = File.ReadAllLines("SmartStoplist.txt").ToList();
            foreach (var item in articles)
            {
                index++;

                dictionaryList.Clear();
                matrixList.Clear();
                keywordCandidateList.Clear();
                scoreList = [];

                s = item?.Content ?? "";
                //richTextBox3.Text += item?.Title + "\n";

                s = s.Replace(",", " ,");
                s = s.Replace(".", " .");
                s = s.Replace("(", "( ");
                s = s.Replace(")", " )");
                s = s.Replace("\"", "\" ");
                s = s.Replace("\"", " \"");
                s = s.Replace("“", "“ ");
                s = s.Replace(":", " :");


                sList = s.Split(' ').ToList();
                removeStopword(ref sList, stopwordList);

                show(sList);

                findDistinctWord(ref dictionaryList, sList);
                createMatrix(ref matrixList, dictionaryList.Count);

                listOutKeywordCandidate(dictionaryList, sList, ref keywordCandidateList, ref matrixList);
                //show(matrixList);
                computeScore(matrixList, dictionaryList, ref scoreList);

                nameScoreList.Add(scoreList);



                var scoreLst = scoreList.Select(s => s.Score).ToList();
                sortList(keywordCandidateList, scoreLst, 'd', dictionaryList);
                show(keywordCandidateList);
                //  calculateSTD(matrixList, scoreLst, dictionaryList);
            }

            // Create a list to store the results
            List<string> results = new List<string>();

            // Create a dictionary to store results for each article
            var articleResults = new Dictionary<int, List<string>>();


            for (int i = 0; i < nameScoreList.Count; i++)
            {
                var mainList = nameScoreList[i].Where(w => w.Score >= 2).ToList();
                var compareList = new List<NameScoreModel>();
                var articleResultsForCurrentArticle = new List<string>();

                for (int j = 0; j < nameScoreList.Count; j++)
                {
                    compareList.Clear();
                    if (i != j)
                    {
                        foreach (var item in mainList)
                        {
                            var score = 0m;
                            if (nameScoreList[j].Any(a => a.Name == item.Name))
                            {
                                score = nameScoreList[j].Where(w => w.Name == item.Name).FirstOrDefault()?.Score ?? 0;
                            }

                            compareList.Add(new NameScoreModel()
                            {
                                Name = item.Name,
                                Score = score
                            });
                        }
                        var result = GetCosineSimilarity(mainList.Select(s => (double)s.Score).ToList(), compareList.Select(s => (double)s.Score).ToList());

                        // Add the result to the list of results for the current article
                        articleResultsForCurrentArticle.Add($"Article {i + 1} With Article {j + 1} - {result.ToString("N2")}");
                    }
                }

                // Sort the results for the current article
                articleResultsForCurrentArticle.Sort((a, b) => decimal.Parse(b.Split('-')[1].Trim()).CompareTo(decimal.Parse(a.Split('-')[1].Trim())));

                // Add the sorted results to the dictionary
                articleResults.Add(i + 1, articleResultsForCurrentArticle);
            }

            // Count the number of articles in the list
            int numberOfArticles = articles.Count;

            // Calculate the top articles
            double topArticles = Math.Log(numberOfArticles);
            int topMatchArticles = Convert.ToInt32(topArticles);

            StringBuilder matchingArticles = new StringBuilder();

            int count = 0; // Counter to track the number of articles processed

            foreach (var article in articleResults)
            {
                // Create a new RelatedArticles instance for the current article
                RelatedArticles relatedArticle = new RelatedArticles();
                relatedArticle.articleTitle = articles[article.Key - 1].Title; // Subtract 1 because the index is zero-based

                // Extract related article indices from the top matching articles
                List<int> relatedIndexList = new List<int>();
                foreach (var result in article.Value.Take(topMatchArticles))
                {
                    int relatedIndex = int.Parse(result.Split("With Article")[1].Split('-')[0].Trim());
                    relatedIndexList.Add(relatedIndex);
                }
                relatedArticle.relatedTitle = relatedIndexList!.Select(index => articles![index - 1]!.Title!).ToList(); // Subtract 1 because the index is zero-based

                // Add the RelatedArticles instance to the list
                relatedArticleList.Add(relatedArticle);

                count++;

                if (count >= topMatchArticles)
                {

                    // Reset the counter
                    count = 0;
                }
            }

            foreach (var kvp in articleResults)
            {
                // Add the article pair to the matching articles string
                matchingArticles.Append($"{kvp.Key} = ");
                matchingArticles.Append(string.Join(",", kvp.Value.Take(topMatchArticles).Select(result => result.Split("With Article")[1].Split('-')[0].Trim()))); // Take topMatchArticles from the current article's results

                matchingArticles.AppendLine();

                count++;
                if (count >= topMatchArticles)
                {
                    // Reset the counter when the desired number of articles is reached
                    count = 0;
                }
            }

            // Write the matching articles to the file
            File.WriteAllText(@"MatchingArticles.txt", matchingArticles.ToString());




        }

        private void removeStopword(ref List<string> sList, List<string> stopwordList)
        {

            double res;
            for (int i = 0; i < sList.Count; i++)
            {
                if (stopwordList.IndexOf(sList[i].ToLower()) >= 0)
                {
                    sList[i] = "";
                }
                else if (double.TryParse(sList[i], out res))
                {
                    sList[i] = "";
                }
                else if (isNumber(sList[i]))
                {
                    sList[i] = "";
                }
            }
        }

        private bool isNumber(string str)
        {
            int count;
            bool status;

            status = false;
            count = 0;

            for (int i = 0; i < str.Length; i++)
            {
                if (char.IsDigit(str[i]) || str[i] == '.' || str[i] == ',')
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            if (count == 1 || count >= 1)
            {
                status = true;
            }

            return (status);

        }

        private void show(List<string> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Console.WriteLine(i + ") [" + list[i] + "]");
                //richTextBox3.Text += i + ") [" + list[i] + "]\n";
            }
            //richTextBox3.Text += "\n";
        }

        private void show(List<List<string>> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = 0; j < list[i].Count; j++)
                {
                    Console.Write("[" + list[i][j] + "] ");
                    //richTextBox3.Text += "[" + list[i][j] + "] ";
                }
                Console.WriteLine();
                //richTextBox3.Text += "\n";
            }
            //richTextBox3.Text += "\n";
        }

        private void findDistinctWord(ref List<string> dictionaryList, List<string> sList)
        {
            //double res;
            for (int i = 0; i < sList.Count; i++)
            {
                if (dictionaryList.IndexOf(sList[i].ToLower()) < 0)
                {
                    if (sList[i].Length > 0)
                    {
                        //if (!double.TryParse(sList[i], out res)){
                        dictionaryList.Add(sList[i].ToLower());
                        //}
                    }
                }
            }
        }

        private void createMatrix(ref List<List<int>> list, int size)
        {
            for (int i = 0; i < size; i++)
            {
                list.Add(new List<int>());
                for (int j = 0; j < size; j++)
                {
                    list[i].Add(0);
                }
            }
        }

        private void listOutKeywordCandidate(List<string> dictionaryList, List<string> sList, ref List<List<string>> keywordCandidateList, ref List<List<int>> matrixList)
        {
            bool status;

            status = false;
            keywordCandidateList.Clear();

            for (int i = 0; i < sList.Count; i++)
            {
                if (sList[i].Length > 0)
                {
                    if (status == false)
                    {
                        keywordCandidateList.Add(new List<string>());
                    }
                    status = true;
                    keywordCandidateList[keywordCandidateList.Count - 1].Add(sList[i].ToLower());
                }
                else
                {
                    status = false;
                }
            }

            for (int i = 0; i < keywordCandidateList.Count; i++)
            {
                for (int j = 0; j < keywordCandidateList[i].Count; j++)
                {
                    matrixList[dictionaryList.IndexOf(keywordCandidateList[i][j])][dictionaryList.IndexOf(keywordCandidateList[i][j])]++;

                    for (int k = j + 1; k < keywordCandidateList[i].Count; k++)
                    {
                        matrixList[dictionaryList.IndexOf(keywordCandidateList[i][j])][dictionaryList.IndexOf(keywordCandidateList[i][k])]++;
                        matrixList[dictionaryList.IndexOf(keywordCandidateList[i][k])][dictionaryList.IndexOf(keywordCandidateList[i][j])]++;
                    }
                }
            }
        }

        private void computeScore(List<List<int>> matrixList, List<string> dictionaryList, ref List<NameScoreModel> scoreList)
        {
            decimal score;

            scoreList.Clear();
            score = 0.00m;
            for (int i = 0; i < matrixList.Count; i++)
            {
                score = matrixList[i].Sum() / (decimal)matrixList[i].Max();
                scoreList.Add(new NameScoreModel()
                {
                    Name = dictionaryList[i],
                    Score = score,
                });
                
            }
        }

        private void sortList(List<List<string>> list, List<decimal> scoreList, char mode, List<string> dictionaryList)
        {
            show(scoreList, dictionaryList);
        }

        private void show(List<decimal> list, List<string> dictionaryList)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Console.WriteLine(i + ") [" + list[i] + "]");
                //richTextBox3.Text += i + ") [" + list[i] + "]\t" + dictionaryList[i] + "\n";
            }
            //richTextBox3.Text += "\n";
        }

        public static double GetCosineSimilarity(List<double> V1, List<double> V2)
        {
            double dot = 0.0d;
            double mag1 = 0.0d;
            double mag2 = 0.0d;
            for (int n = 0; n < V1.Count; n++)
            {
                dot += V1[n] * V2[n];
                mag1 += Math.Pow(V1[n], 2);
                mag2 += Math.Pow(V2[n], 2);
            }

            var result = dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));

            return double.IsNaN(result) ? 0 : result;
        }

        private static List<string> ReadExistingTitles(string filePath)
        {
            List<string> titles = new List<string>();

            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);

                foreach (string line in lines)
                {
                    string[] parts = line.Split('■');
                    if (parts.Length > 0)
                    {
                        titles.Add(parts[0]);
                    }
                }
            }

            return titles;
        }

        private void DisplayRelatedArticles(string? selectedTitle)
        {
            // Find the RelatedArticles instance corresponding to the selected article title
            var relatedArticle = relatedArticleList.FirstOrDefault(ra => ra.articleTitle == selectedTitle);

            // Check if the related articles instance exists
            if (relatedArticle != null)
            {

                // Clear the items in the relatedList
                relatedList.Items.Clear();

                // Add the related articles' titles to the relatedList

                if (relatedArticle.relatedTitle != null)
                {
                    foreach (string title in relatedArticle.relatedTitle)
                    {
                        relatedList.Items.Add(title);
                    }
                }
            }
        }

        private void relatedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Get the selected related article title from the ListBox
            string? selectedTitle = relatedList.SelectedItem?.ToString();

            // Set the selected related article title to the comboBox
            comboBox1.SelectedItem = selectedTitle;
        }

        List<RelatedArticles> relatedArticleList = new List<RelatedArticles>(); // Create a list to store related articles data

        List<Article> articles = new List<Article>();

        List<Company> companyList = new List<Company>();

        class Article
        {
            public string? Title { get; set; }
            public string? Content { get; set; }
            public string? Date { get; set; }
        }


        class Company
        {
            public string? Name { get; set; }
            public string? Description { get; set; }

            public string? CompanyCode { get; set; }

            public List<NameScoreModel> Keywords { get; set; } = new List<NameScoreModel>();
        }

        public class Prop
        {
            public PageProp? props { get; set; }
        }

        public class PageProp
        {
            public AllArticleModel? pageProps { get; set; }
        }

        public class AllArticleModel
        {
            public List<ArticleModel>? homeData { get; set; }
        }
        public class ArticleModel
        {
            //public Int64? nid { get; set; }
            public string? title { get; set; }
            public string? alias { get; set; }

            public long updated { get; set; }
        }

        public class NameScoreModel
        {
            public string Name { get; set; } = null!;
            public decimal Score { get; set; }
        }

        public class RelatedArticles
        {
            public string? articleTitle { get; set; }

            public List<string>? relatedTitle { get; set; }
        }

        public class CompanySimilarity
        {
            public int CompanyIndex { get; set; }
            public double Similarity { get; set; }
        }

        public class StockData
        {
            public DateTime Date { get; set; }
            public float Open { get; set; }
            public float High { get; set; }
            public float Low { get; set; }
            public float Close { get; set; }
            public float AdjClose { get; set; }
            public float Volume { get; set; }
        }


    }
}
