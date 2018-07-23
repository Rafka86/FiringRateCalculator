using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using static System.Console;

namespace FiringRateCalculator {

  internal static class Program {
    private static void Main(string[] args) {
      var givenSettingFile = false;
      var skipOk = false;
      var settingFile = "";
      CheckArgs();

      var endTime = 1000.0;
      var timeBin = 1000.0;
      var samplingRate = 0.03;
      var neuronSize = 0;
      var fileLoadCommands = new List<string>();
      var configCommands = new List<string>();
      var outputCommands = new List<string>();
      using (var archive = ZipFile.OpenRead(args[0])) {
        if (givenSettingFile) LoadSettingFiles();
        var list = ChooseFiles(archive);
        Configure();
        var spikeList = LoadFiles(archive, list);
        var firingRates = Calculation(spikeList);
        OutputToFiles(firingRates);
      }
      WriteLine("プログラムを終了します．");
      
      void CheckArgs() {
        var sb = new StringBuilder();
        sb.AppendLine("Usage : $ ./FiringRateCalculator [FILE].zip [options]\n");
        sb.AppendLine("[options]");
        sb.AppendLine("\t-h|--help : Helpを表示して終了する．");
        sb.AppendLine("\t-s [FILE] : 設定ファイルを指定する．");
        sb.Append    ("\t-y        : 自動で\"ok\"を入力する．");
        if (args.Length == 0 || args.Length > 4)
          throw new ArgumentException(sb.ToString());

        for (var i = 1; i < args.Length; i++) {
          switch (args[i]) {
            case "-h": case "--help": WriteLine(sb.ToString()); Environment.Exit(0); break;
            case "-s": givenSettingFile = true; settingFile = args[i + 1]; break;
            case "-y": skipOk = true; break;
          }
        }
      }

      void LoadSettingFiles() {
        WriteLine($"設定ファイル\"{settingFile}\"を読み込みます．");
        using (var sr = new StreamReader(settingFile)) {
          string line;
          while ((line = sr.ReadLine()) != null) {
            var coms = line.Split();
            switch (coms[0].ToLower()) {
              case "fileload": fileLoadCommands.Add(line); break;
              case "endtime":
              case "timebin":
              case "samplingrate":
              case "numberofneurons": configCommands.Add(line); break;
              case "output": outputCommands.Add(line); break;
            }
          }
        }
        WriteLine("設定ファイルの読み込みが完了しました．");
      }

      string[] ChooseFiles(ZipArchive archive) {
        WriteLine($"\"{args[0]}\"には以下のファイルが含まれています．"); 
        var sb = new StringBuilder();
        var lineCount = 0;
        foreach (var file in archive.Entries) {
          if (lineCount + file.Name.Length >= 80) {
            sb.Append("\n");
            lineCount = 0;
          }
          sb.Append(file.Name + " ");
          lineCount += file.Name.Length + 1;
        }
        var fileList = sb.ToString();
        WriteLine(fileList);
        
        WriteLine("計算対象とするファイルの一覧を作成します．");
        var res = new List<string>();

        if (fileLoadCommands.Count != 0) {
          WriteLine($"設定ファイル\"{args[1]}\"から以下のファイル指定を読み込みます．");
          foreach (var command in fileLoadCommands) {
            var comArgs = command.Split();
            switch (comArgs[0].ToLower()) {
              case "fileload": AddIndex(comArgs); break;
              default: WriteLine($"{comArgs[0]}は未定義のコマンドです．"); break;
            }
          }
          WriteLine("以下のファイル指定を読み込みました．");
          WriteLine(string.Join('\n', res));
        }
        
        WriteLine("使用可能なコマンドを以下に示します．");
        var sbHelp = new StringBuilder();
        sbHelp.AppendLine("list [ls]          : zipファイル内のファイル一覧を表示．");
        sbHelp.AppendLine("add {file}         : {file} を一覧に追加．");
        sbHelp.AppendLine("remove [rm] {file} : {file} を一覧から削除．");
        sbHelp.AppendLine("chlist [cls]       : 一覧を表示．");
        sbHelp.AppendLine("help [h]           : これを表示．");
        sbHelp.Append    ("ok                 : 操作を完了する．");
        var helpString = sbHelp.ToString();
        WriteLine(helpString);
        
        string str;
        Write(">> ");
        while (!skipOk && (str = ReadLine()) != "ok") {
          if (str == null) continue;
          var coms = str.Split();
          switch (coms[0].ToLower()) {
            case "ls": case "list": WriteLine(fileList); break;
            case "add": AddIndex(coms); break;
            case "chlist": case "cls": WriteLine(string.Join('\n', res)); break;
            case "remove": case "rm": RemoveIndex(coms); break;
            case "help": case "h": WriteLine(helpString); break;
            default: WriteLine("使用可能なコマンドではありません．"); break;
          }
          Write(">> ");
        }

        if (skipOk) WriteLine("ok");
        WriteLine("計算対象のファイルとして，以下のファイルを選択しました．");
        WriteLine(string.Join('\n', res));
        return res.ToArray();

        void AddIndex(IReadOnlyList<string> comArgs) {
          if (comArgs.Count == 2 && fileList.Contains(comArgs[1])) res.Add(comArgs[1]);
          else WriteLine($"ファイル\"{comArgs[1]}\"はありません");
        }

        void RemoveIndex(IReadOnlyList<string> comArgs) {
          if (comArgs.Count == 2 && res.Contains(comArgs[1])) res.Remove(comArgs[1]);
          else WriteLine($"ファイル\"{comArgs[1]}\"はありません");
        }
      }

      void Configure() {
        WriteLine("発火率計算のための情報を設定します．");
        WriteLine("入力された値はそのまま使用されるので，データの単位と合うように入力してください．");
        var loadFlags = new Dictionary<string, bool> {
                                                       {"endtime", false},
                                                       {"timebin", false},
                                                       {"samplingrate", false},
                                                       {"numberofneurons", false}
                                                     };
        if (configCommands.Count != 0) {
          WriteLine($"設定を\"{args[1]}\"から読み込みます．");
          foreach (var command in configCommands) {
            var comArgs = command.Split();
            switch (comArgs[0].ToLower()) {
              case "endtime":
                double.TryParse(comArgs[1], out endTime);
                loadFlags["endtime"] = true;
                break;
              case "timebin":
                double.TryParse(comArgs[1], out timeBin);
                loadFlags["timebin"] = true;
                break;
              case "samplingrate":
                double.TryParse(comArgs[1], out samplingRate);
                loadFlags["samplingrate"] = true;
                break;
              case "numberofneurons":
                int.TryParse(comArgs[1], out neuronSize);
                loadFlags["numberofneurons"] = true;
                break;
              default:
                WriteLine($"{comArgs[0]}は未定義のコマンドです．");
                break;
            }
          }
        }

        Write("データの最終時刻 : ");
        if (loadFlags["endtime"]) WriteLine(endTime);
        else double.TryParse(ReadLine(), out endTime);
        Write("時間窓の幅 : ");
        if (loadFlags["timebin"]) WriteLine(timeBin);
        else double.TryParse(ReadLine(), out timeBin);
        Write("サンプリングレート : ");
        if (loadFlags["samplingrate"]) WriteLine(samplingRate);
        else double.TryParse(ReadLine(), out samplingRate);
        Write("ニューロンの総数 : ");
        if (loadFlags["numberofneurons"]) WriteLine(samplingRate);
        else int.TryParse(ReadLine(), out neuronSize);
      }

      Dictionary<string, double[][]> LoadFiles(ZipArchive archive, IEnumerable<string> fileList) {
        WriteLine("データの読み込みを開始します．");
        var res = new Dictionary<string, double[][]>();
        foreach (var fileName in fileList) {
          var entry = archive.GetEntry(fileName);
          if (entry == null) continue;
          var tmpList = new List<double>[neuronSize];
          for (var i = 0; i < tmpList.Length; i++) tmpList[i] = new List<double>();
          using (var sr = new StreamReader(entry.Open())) {
            string strs;
            while ((strs = sr.ReadLine()) != null) {
              var data = strs.Split().Select(double.Parse).ToArray();
              tmpList[(int) data[1]].Add(data[0]);
            }
          }

          var spikes = new double[neuronSize][];
          for (var i = 0; i < spikes.Length; i++) spikes[i] = tmpList[i].ToArray();
          res.Add(fileName, spikes);
        }

        WriteLine("完了しました．");
        return res;
      }

      Dictionary<string, double[,]> Calculation(Dictionary<string, double[][]> spikes) {
        WriteLine("発火率の計算を開始します．");
        var res = new Dictionary<string, double[,]>();
        var count = 0;
        var delta = 1.0 / samplingRate;
        for (var t = delta; t <= endTime; t += delta) count++;
        var tbHalf = timeBin * 0.5;

        foreach (var data in spikes) {
          var loadSpks = data.Value;
          var frates = new double[count, neuronSize];
          Parallel.For(0, neuronSize, i => {
                                        var lc         = 0;
                                        var neuronSpks = loadSpks[i];
                                        for (var t = delta; t <= endTime; t += delta) {
                                          var spkCount = 0;
                                          for (var j = 0; j < neuronSpks.Length && neuronSpks[j] <= t + tbHalf; j++) {
                                            if (neuronSpks[j] < t - tbHalf) continue;
                                            spkCount++;
                                          }

                                          frates[lc++, i] = spkCount / timeBin;
                                        }
                                      });
          res.Add(data.Key, frates);
        }
        
        WriteLine("完了しました．");
        return res;
      }

      void OutputToFiles(Dictionary<string, double[,]> firingRates) {
        WriteLine("結果をファイルに出力します．");
        if (!Directory.Exists("output")) Directory.CreateDirectory("output");
        var delta = 1.0 / samplingRate;

        using (var maxFrateFile = new StreamWriter(@"output/max.out")) {
          if (outputCommands.Count != 0) {
            WriteLine($"設定ファイル\"{args[1]}\"に従って出力を行います．");
            foreach (var command in outputCommands) {
              var comArgs = command.Split();
              switch (comArgs[0].ToLower()) {
                case "output": OutputToFile(maxFrateFile, new ArraySegment<string>(comArgs, 1, 2)); break;
                default: WriteLine($"{comArgs[0]}は未定義のコマンドです．"); break;
              }
            }
            WriteLine("ファイルからの出力が完了しました．");
            Write("追加の処理があれば，");
          }
          var sb = new StringBuilder();
          sb.AppendLine("以下のフォーマットに従って，ニューロンIDの登録を行ってください．");
          sb.AppendLine(">> Name [StartID]:[EndID]");
          sb.AppendLine("Example : >> Gr 0:1023");
          sb.Append    ("操作を終了するには\"ok\"を入力してください．");
          var helpString = sb.ToString();
          WriteLine(helpString);
          Write(">> ");
          string strs;
          while (!skipOk && (strs = ReadLine()) != "ok") {
            if (strs == null) continue;
            var coms = strs.Split();
            if (coms[0].ToLower() == "help" || coms[0].ToLower() == "h") {
              WriteLine(helpString);
            } else OutputToFile(maxFrateFile, coms);
            Write(">> ");
          }
          if (skipOk) WriteLine("ok");
        }

        void OutputToFile(TextWriter maxFile, IReadOnlyList<string> comArgs) {
          if (comArgs.Count != 2) {
            WriteLine("不正なフォーマットです．");
            return;
          }

          var ids     = comArgs[1].Split(':').Select(int.Parse).ToArray();
          var startId = ids[0];
          var endId   = ids[1];
          if ((uint) startId >= neuronSize || (uint) endId >= neuronSize) {
            WriteLine("IDの指定が与えられたデータの範囲を超えています．");
            return;
          }

          var maxFrate = 0.0;
          foreach (var data in firingRates) {
            var frates = data.Value;
            using (var sw = new StreamWriter($@"output/{comArgs[0]}.frate.{data.Key}")) {
              WriteLine($@"output/{comArgs[0]}.frate.{data.Key} に出力中．");
              for (var i = 0; i < frates.GetLength(0); i++) {
                sw.Write(delta * (i + 1));
                for (var j = startId; j <= endId; j++) {
                  maxFrate = frates[i, j] > maxFrate ? frates[i, j] : maxFrate;
                  sw.Write($" {frates[i, j]}");
                }

                sw.WriteLine();
              }
            }
          }

          maxFile.WriteLine($"{comArgs[0]} {maxFrate}");
        }
      }
    }
  }

}