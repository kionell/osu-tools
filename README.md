# Edited Readme, because I know you are here for the pp rework calc 

## Last update: 15/11/2020

# Setup
- Either download the source zip of this repo or install `git` and clone it yourself
- Replace the `osu` folder contents with the **xexxar** rework (located [here](https://github.com/Apollo-P/osu/tree/PP)) or delta's rework 
- Install `.NetCore v3.1` or higher
- Unzip the contents of `patch.zip` in the repository folder

# Recalc all of your local ranked scores

The command will read all of your **LOCAL** replays and use your **LOCAL** beatmaps to recalc all of your scores and give you a new top 500.

This means that any map you dont have the replays for / any map you dont have on disk will get ignored.

For the recalc, **pp > Score** *(screw you, scorev1)*

To my knowledge, I cannot display the diff with live pp without basically ddosing bancho in the process. For the same reasons, 
I cannot do a full recalc based on your online scores.

### **USE -t TO DO A TEST RUN BEFORE RUNNING THE LONG FULL COMMAND**
*or dont complain if your command fails after 20mins of processing because you entered invalid args*

### **BE SURE TO USE THE COMMAND ON BACKUPS OF YOUR .db FILES**

The command is read only on the databases (obviously) but you can never be too safe.

To run, execute the command below in the `PerformanceCalculator` folder.

```
> dotnet run -- localscores --help

Recalcs all of your local scores

Usage: dotnet run -- localscores [options] <osuDb> <scoresDb> <songsFolderPath> <userName>

Arguments:
  osuDb                          Path to your osu!.db file. Copy and rename it
                                 to remove the '!' in the filename if you have
                                 issues.
  scoresDb                       Path to your scores.db file.
  songsFolderPath                Path to your osu Songs folder.
  userName                       Your own username as shown in your local
                                 leaderboards. Use the -u option to process
                                 extra usernames.

Options:
  -?|-h|--help                   Show help information
  -u|--user <username>           Extra usernames to process
  -c|--columns <attribute_name>  Extra columns to display from category attribs,
                                 for example 'Tap Rhythm pp'
  -s|--sort <attribute_name>     What column to sort by (defaults to pp of the
                                 play)
  -t|--test-run                  Only run on 20 beatmaps to test your arguments
  -o|--output <file.txt>         Output results to text file.
```

#### Sample command to test run for the 3 usernames Sriki, Srikiki and otherUserName, sorting by acc pp, displaying extra attribute "Aim Flow pp":

`dotnet run -- localscores "D:/Dev/osu.db" "D:/Dev/scores.db"  D:/Games/osu/Songs Sriki -u Srikiki -u otherUserName -s "Accuracy pp" -c "Aim Flow pp" -t -o "D:/Dev/osu-tools/localscores.txt"`

#### If your username contains this character -> - 
The CLI library cant handle it properly as an argument, so use a placeholder for the username arg and add the `-u` option like that: `-u="-MyUserName-"`

# Run other pp commands
- Read about the other available commands here: [PerformanceCalculator Readme](https://github.com/ppy/osu-tools/blob/master/PerformanceCalculator/README.md).

