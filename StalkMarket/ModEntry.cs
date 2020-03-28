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

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += this.SaveLoaded;
        }
        

        private void SaveLoaded(object sender, EventArgs e)
        {
            // Check if we have a week pattern set
            var model = this.Helper.Data.ReadJsonFile<ModData>("dat/{Constants.SaveFolderName}.json") ?? new ModData();

            // If not, set one at random
            if(model.pattern == "none")
            {
                this.Monitor.Log("No pattern set. Setting...", LogLevel.Debug);
                int rnd = random.Next(0, 3);

                switch(rnd)
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
            }

            this.Monitor.Log("Pattern set to "+model.pattern, LogLevel.Debug);
            
            // Generate prices according to pattern
            switch(model.pattern)
            {
                case "rnd":
                    // Sequence of 3 consecutive falling prices below 80
                    int rnd = random.Next(50, 80);
                    model.weekPrices[0] = rnd;

                    // Just ignore the probability that this may be 2x50
                    for (int i = 1; i <= 2; i++)                                            
                        model.weekPrices[i] = random.Next(50, model.weekPrices[i - 1]);                    

                    // Small spike to 110-600
                    model.weekPrices[3] = random.Next(110, 600);

                    // next two drop by 4-12+ bells
                    for (int i=4; i<=5; i++)                    
                        model.weekPrices[i] = model.weekPrices[i-1] - (random.Next(4, 20));

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

                    if(model.pattern == "sml")
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
                    for(int i=1; i<high; i++)
                    {
                        int curPrice = model.weekPrices[i];
                        int lastPrice = model.weekPrices[i - 1];
                        int highPrice = model.weekPrices[high];

                        // If high point is near, do big increases - min = lastPrice + 20%, max = highPrice - 10%
                        // If not, increase 5-10%
                        if (i + 2 == high || i + 1 == high)
                            model.weekPrices[i] = random.Next(lastPrice + ((highPrice - lastPrice) / 100) * highPerc, highPrice - (highPrice / 100) * lowPerc);
                        else
                            model.weekPrices[i] = random.Next(lastPrice + ((highPrice - lastPrice) / 100) * lowPerc, highPrice - (highPrice / 100) * lowestPerc);                        
                    }

                    // decreases from high to end, 5-10% each step, first step 10-20%
                    for (int i = (high + 1); i <= 11; i++)
                    {
                        // 504 - (504 / 100) * 20 = 403,2
                        if (i == (high + 1))
                            model.weekPrices[i] = random.Next(model.weekPrices[high] - (model.weekPrices[high] / 100) * highPerc, model.weekPrices[high] - (model.weekPrices[high] / 100) * lowPerc);
                        else
                            model.weekPrices[i] = random.Next(model.weekPrices[i - 1] - (model.weekPrices[i - 1] / 100) * lowPerc, model.weekPrices[i - 1] - (model.weekPrices[i - 1] / 100) * lowestPerc);
                    }
                    break;
            }

            for(int i=0; i<=11; i++)            
                this.Monitor.Log("Price: "+model.weekPrices[i], LogLevel.Debug);
            
            // TODO: Set prices at day start and midday, save prices to file on exit

            // Set the new turnip price for the day
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
            
            // Randomize the turnip price
            Game1.objectInformation.Remove(theKey);

            int price = random.Next(25, 300);
            String strPrice = price.ToString();

            Game1.objectInformation.Add(theKey, "Turnip/"+strPrice+"/-300/Basic -79/Turnip/Looks like a regular turnip.");

            foreach (System.Collections.Generic.KeyValuePair<int, string> kvp in Game1.objectInformation)
            {
                if (kvp.Key == theKey)
                    this.Monitor.Log(kvp.ToString(), LogLevel.Debug);

            }
        }        
    }
}