using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;

namespace StalkMarket
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        Random random = new Random();
        ModData model = null;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += this.SaveLoaded;
            helper.Events.GameLoop.DayStarted += this.DayStarted;
            helper.Events.GameLoop.TimeChanged += this.TimeChanged;
            helper.Events.GameLoop.Saved += this.Saved;
            model = this.Helper.Data.ReadJsonFile<ModData>($"dat/{Constants.SaveFolderName}.json") ?? new ModData();
        }

        // Generates turnip prices for a whole week
        private void GenerateWeeklyPrices()
        {
            // Choose overall pattern
            int rnd = random.Next(0, 3);

            switch (rnd)
            {
                case 0:
                    model.pattern = "rnd";
                    break;

                case 1:
                    model.pattern = "dec";
                    break;

                case 2:
                    model.pattern = "lrg";
                    break;

                case 3:
                    model.pattern = "sml";
                    break;

                default:
                    model.pattern = "none";
                    break;
            }


            this.Monitor.Log("Pattern set to " + model.pattern, LogLevel.Debug);

            // Generate prices according to pattern
            switch (model.pattern)
            {
                case "rnd":
                    // Sequence of 3 consecutive falling prices below 80                        
                    model.weekPrices[0] = random.Next(50, 80);

                    // Just ignore the probability that this may be 2x50
                    for (int i = 1; i <= 2; i++)
                        model.weekPrices[i] = random.Next(50, model.weekPrices[i - 1]);

                    // Small spike to 110-600
                    model.weekPrices[3] = random.Next(110, 600);

                    // next two drop by 4-12+ bells
                    for (int i = 4; i <= 5; i++)
                        model.weekPrices[i] = model.weekPrices[i - 1] - (random.Next(4, 20));

                    // otherwise random prices from 40-600
                    for (int i = 6; i <= 11; i++)
                        model.weekPrices[i] = random.Next(40, 600);
                    break;

                case "dec":
                    // Values decrease 3-5 twice a day                    
                    model.weekPrices[0] = random.Next(90, 110);

                    for (int i = 1; i <= 11; i++)
                        model.weekPrices[i] = model.weekPrices[i - 1] - (random.Next(3, 5));
                    break;

                case "lrg":
                case "sml":
                    int highestPerc = 40;
                    int highPerc = 20;
                    int lowPerc = 10;
                    int lowestPerc = 5;

                    if (model.pattern == "sml")
                    {
                        highestPerc = 20;
                        highPerc = 10;
                        lowPerc = 5;
                        lowestPerc = 2;
                    }
                    // 5 consecutive numbers somewhere in the week
                    // 3 large increases followed by 2 large decreases

                    // first, pick the high point and generate its value
                    int high = random.Next(2, 9);
                    model.weekPrices[high] = random.Next(110, 600);

                    // generate the surrounding increases/decreases
                    // Start the first value at least 20% below the maximum  to give some space for the increases
                    model.weekPrices[0] = random.Next(model.weekPrices[high] - (model.weekPrices[high] / 100) * highestPerc, model.weekPrices[high] - (model.weekPrices[high] / 100) * highPerc);

                    // increases up to high
                    for (int i = 1; i < high; i++)
                    {
                        int curPrice = model.weekPrices[i];
                        int lastPrice = model.weekPrices[i - 1];
                        int highPrice = model.weekPrices[high];

                        // If high point is near, do big increases - min = lastPrice + 20%, max = highPrice - 10%
                        // If not, increase 5-10%
                        if (i + 2 == high || i + 1 == high)
                        {
                            int a = lastPrice + (lastPrice / 100) * highPerc;
                            int b = highPrice - (highPrice / 100) * lowPerc;
                            int tmp = 0;

                            // if min > max, swap
                            if (a > b)
                            {
                                tmp = a;
                                a = b;
                                b = tmp;
                            }

                            model.weekPrices[i] = random.Next(a, b);
                        }
                        else
                        {
                            int a = lastPrice + (lastPrice / 100) * lowPerc;
                            int b = highPrice - (highPrice / 100) * lowestPerc;
                            int tmp = 0;

                            if(a > b)
                            {
                                tmp = a;
                                a = b;
                                b = tmp;
                            }

                            model.weekPrices[i] = random.Next(a, b);
                        }
                    }

                    // decreases from high to end, 5-10% each step, first step 10-20%
                    for (int i = (high + 1); i <= 11; i++)
                    {                        
                        if (i == (high + 1))
                            model.weekPrices[i] = random.Next(model.weekPrices[high] - (model.weekPrices[high] / 100) * highPerc, model.weekPrices[high] - (model.weekPrices[high] / 100) * lowPerc);
                        else
                            model.weekPrices[i] = random.Next(model.weekPrices[i - 1] - (model.weekPrices[i - 1] / 100) * lowPerc, model.weekPrices[i - 1] - (model.weekPrices[i - 1] / 100) * lowestPerc);
                    }
                    break;
            }
        }
        
        // Updates the turnip price
        private void SetNewTurnipPrice()
        {            
            // Find the turnip key
            int theKey = -1;
            foreach (System.Collections.Generic.KeyValuePair<int, string> kvp in Game1.objectInformation)
            {
                if (kvp.Value.StartsWith("Turnip"))
                {
                    theKey = kvp.Key;
                    this.Monitor.Log(kvp.ToString(), LogLevel.Debug);
                }

            }

            // Set the new turnip price
            Game1.objectInformation.Remove(theKey);

            // TODO: Check which day and time we are and set the turnip price accordingly
            int dom = Game1.dayOfMonth;            
            int tod = Game1.timeOfDay;
            int price = 0;

            switch(dom)
            {
                // Monday
                case 1:
                case 8:
                case 15:
                case 22:
                    if (tod < 1200)
                        price = model.weekPrices[0];
                    else
                        price = model.weekPrices[1];
                    break;

                // Tuesday
                case 2:
                case 9:
                case 16:
                case 23:
                    if (tod < 1200)
                        price = model.weekPrices[2];
                    else
                        price = model.weekPrices[3];
                    break;

                // Wednesday
                case 3:
                case 10:
                case 17:
                case 24:
                    if (tod < 1200)
                        price = model.weekPrices[4];
                    else
                        price = model.weekPrices[5];
                    break;

                // Thursday
                case 4:
                case 11:
                case 18:
                case 25:
                    if (tod < 1200)
                        price = model.weekPrices[6];
                    else
                        price = model.weekPrices[7];
                    break;

                // Friday
                case 5:
                case 12:
                case 19:
                case 26:
                    if (tod < 1200)
                        price = model.weekPrices[8];
                    else
                        price = model.weekPrices[9];
                    break;

                // Saturday
                case 6:
                case 13:
                case 20:
                case 27:
                    if (tod < 1200)
                        price = model.weekPrices[10];
                    else
                        price = model.weekPrices[11];
                    break;

                // Sunday
                default:
                    price = random.Next(90, 110)/2;
                    break;
            }

            this.Monitor.Log("It's the "+dom.ToString()+". day of the month and "+tod.ToString()+" hours.", LogLevel.Debug);

            String strPrice = price.ToString();

            Game1.objectInformation.Add(theKey, "Turnip/" + strPrice + "/-300/Basic -79/Turnip/Looks like a regular turnip.");

            foreach (System.Collections.Generic.KeyValuePair<int, string> kvp in Game1.objectInformation)
            {
                if (kvp.Key == theKey)
                    this.Monitor.Log(kvp.ToString(), LogLevel.Debug);

            }
        }

        private void ActivateMarket()
        {
            // Add the Stalk Market to the Sewer Map
            GameLocation sewer = Game1.getLocationFromName("Farm");
            // 13, 10
            int tileX = 63;
            int tileY = 14;            

            // add Shop property            
            if (Game1.dayOfMonth % 7 == 0)
                sewer.setTileProperty(tileX, tileY, "Buildings", "Shop", "Stalk Market");
            else
                sewer.setTileProperty(tileX, tileY, "Buildings", "Shop", "Stalk Market 2");

            sewer.setTileProperty(tileX, tileY, "Buildings", "Action", "");

            // TODO: Instead of stockless shop, add some dialog for non-Sundays
        }
        
        // Check if there's a weekly pattern. If this is the first time someone loads this mod, there's none - generate it and set prices
        private void SaveLoaded(object sender, EventArgs e)
        {            
            if (model.pattern == "none")
            {
                this.Monitor.Log("No pattern set. Setting...", LogLevel.Debug);
                GenerateWeeklyPrices();            

                // DEBUG
                for (int i = 0; i <= 11; i++)
                    this.Monitor.Log("Price: " + model.weekPrices[i], LogLevel.Debug);                
            }           
        }

        // On Mondays, generate a new weekly pattern
        // Set prices for the morning
        private void DayStarted(object sender, EventArgs e)
        {
            if(Game1.dayOfMonth == 1 || Game1.dayOfMonth == 8 || Game1.dayOfMonth == 15 || Game1.dayOfMonth == 22)
            {
                this.Monitor.Log("It's a Monday. Setting...", LogLevel.Debug);
                GenerateWeeklyPrices();

                // DEBUG
                for (int i = 0; i <= 11; i++)
                    this.Monitor.Log("Price: " + model.weekPrices[i], LogLevel.Debug);                
            }

            SetNewTurnipPrice();
            ActivateMarket();

            // TODO: Do this on day ending so that new prices are set correctly in the morning?
        }

        // If it's noon, update price
        private void TimeChanged(object sender, EventArgs e)
        {
            if (Game1.timeOfDay == 1200)
                SetNewTurnipPrice();
        }

        // Save prices and price pattern to file on exit
        private void Saved(object sender, EventArgs e)
        {
            this.Helper.Data.WriteJsonFile($"dat/{Constants.SaveFolderName}.json", model);
            this.Monitor.Log("Saved stalk data.", LogLevel.Debug);
        }
    }
}