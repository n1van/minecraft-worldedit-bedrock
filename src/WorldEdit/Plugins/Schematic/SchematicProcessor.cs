using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ShapeGenerator;
using ShapeGenerator.Generators;
using WorldEdit.Output;

namespace WorldEdit.Schematic
{
    public class SchematicProcessor
    {
        private readonly IMinecraftCommandService _minecraftCommandService;

        public SchematicProcessor(IMinecraftCommandService minecraftCommandService)
        {
            _minecraftCommandService = minecraftCommandService;
        }

        public void SchematicCommandProcessor(string[] args)
        {
            //   var s = new MinecraftCodeConnectionCommandService();
            var target = new Position(0, 0, 0);
            var shift = new Position(0, 0, 0);
            var rotation = Rotate.None;

            var command = args[0];


            switch (command)
            {
                case "list":
                    var files = Directory.GetFiles(ConfigurationManager.AppSettings["data"], "*.schematic");

                    _minecraftCommandService
                        .Status("Schematics: " +
                                files.Select(b => $"\n" + Path.GetFileName(b))
                                    .OrderBy(a => a)
                                    .Aggregate((a, b) => a += b));

                    break;
                case "analyze":
                {
                    var points = LoadFile(args[1]);
                    var results = ModelAnalyzer.Analyze(points);
                    var firstGroundLayer =
                        results.Layers.First(a => a.Blocks.Any(b => b.Block.Equals("air") && b.PercentOfLayer >= 0.5))
                            .Y;
                    string output =
                        $"{Path.GetFileName(args[1])} Model Size: X:{results.Width} Y:{results.Height} Z:{results.Length} Ground Level:{firstGroundLayer} Total Blocks:{results.Width*results.Height*results.Length}";

                    _minecraftCommandService.Status(output);
                    break;
                }
                case "outline":
                    Outline(args, target, shift);
                    break;
                case "import":
                    Import(args, target, rotation, shift);
                    break;
                case "test":
                    //analyze then import all schematics in the folder.
                    var x = int.Parse(args[1]);
                    var y = int.Parse(args[2]);
                    var z = int.Parse(args[3]);
                    var files1 = Directory.GetFiles(ConfigurationManager.AppSettings["data"], "*.schematic");
                    var filesToProcess = files1.Select(a => new {Points = LoadFile(a), Filename = a})
                        .ToList()
                        .Select(a => new {a.Filename, a.Points, Analysis = ModelAnalyzer.Analyze(a.Points)})
                        .OrderBy(a => a.Analysis.TotalPlaceableBlocks)
                        .ToList();
                    foreach (var f in filesToProcess)
                    {
                        target.X = x;
                        target.Y = y;
                        target.Z = z;

                        _minecraftCommandService.Command(
                            $"tp @s {x + f.Analysis.Width/2} {y + f.Analysis.Height} {z - 5}");
                        _minecraftCommandService.Status($"importing {Path.GetFileName(f.Filename)}");
                        SendCommandsToCodeConnection(target, f.Points, rotation, shift);
                        var results = f.Analysis;
                        x = x + results.Width + 15;
                    }


                    break;
                default:
                    _minecraftCommandService.Status("schematic command\n" +
                                                    "schematic list\n" +
                                                    "schematic analyze [name]\n" +
                                                    "schematic import name x y z (rotation) (Shift X) (Shift Y) (Shift Z)");
                    break;
            }
        }

        private void Outline(string[] args, Position target, Position shift)
        {
            Rotate rotation;
            var FileName = args[1];
            if (args.Length == 6)
            {
                target.X = Convert.ToInt32(args[3]);
                target.Y = Convert.ToInt32(args[4]);
                target.Z = Convert.ToInt32(args[5]);
            }
            if (args.Length >= 5)
            {
                if (args[2].StartsWith("~") || args[3].StartsWith("~") || args[4].StartsWith("~"))
                {
                    //get current position
                    //parse and add to current position.
                    _minecraftCommandService.Status("relative positions are not supported.");
                    return;
                }
                target.X = Convert.ToInt32(args[2]);
                target.Y = Convert.ToInt32(args[3]);
                target.Z = Convert.ToInt32(args[4]);
            }
            if (args.Length >= 6)
            {
                rotation = (Rotate) Convert.ToInt32(args?[5]);
            }
            if (args.Length >= 9)
            {
                shift.X = Convert.ToInt32(args[6]);
                shift.Y = Convert.ToInt32(args[7]);
                shift.Z = Convert.ToInt32(args[8]);
            }

            Console.WriteLine($"outlineing {FileName} to {target}");
            var points = LoadFile(FileName);
            var results = ModelAnalyzer.Analyze(points);
            var x= (target.X + results.Width /2).ToString();
            var z = (target.Z + results.Length/2).ToString();
            CreateHandler.CreateGeometry(_minecraftCommandService,"create","box",results.Width.ToString(),results.Length.ToString(),results.Height.ToString(),"wool",x,target.Y.ToString(),z);
        }

        private void Import(string[] args, Position target, Rotate rotation, Position shift)
        {
            var FileName = args[1];
            if (args.Length == 6)
            {
                target.X = Convert.ToInt32(args[3]);
                target.Y = Convert.ToInt32(args[4]);
                target.Z = Convert.ToInt32(args[5]);
            }
            if (args.Length >= 5)
            {
                if (args[2].StartsWith("~") || args[3].StartsWith("~") || args[4].StartsWith("~"))
                {
                    //get current position
                    //parse and add to current position.
                    _minecraftCommandService.Status("relative positions are not supported.");
                    return;
                }
                target.X = Convert.ToInt32(args[2]);
                target.Y = Convert.ToInt32(args[3]);
                target.Z = Convert.ToInt32(args[4]);
            }
            if (args.Length >= 6)
            {
                rotation = (Rotate) Convert.ToInt32(args?[5]);
            }
            if (args.Length >= 9)
            {
                shift.X = Convert.ToInt32(args[6]);
                shift.Y = Convert.ToInt32(args[7]);
                shift.Z = Convert.ToInt32(args[8]);
            }

            Console.WriteLine($"importing {FileName} to {target}");
            var points1 = LoadFile(FileName);
            SendCommandsToCodeConnection(target, points1, rotation, shift);
        }

        private static List<Point> LoadFile(string FileName)
        {
            var points = new List<Point>();
            Schematic schematic;
            if (!FileName.EndsWith("schematic"))
            {
                FileName += ".schematic";
            }
            var combine = Path.Combine(ConfigurationManager.AppSettings["data"], FileName);
            if (!File.Exists(FileName) && File.Exists(combine))
            {
                FileName = combine;
            }
            schematic = Schematic.LoadFromFile(FileName);
            points = schematic.GetPoints();
            //var outputFilename = Path.GetFileNameWithoutExtension(FileName) + ".fill";
            //if (args.Length > 2)
            //{
            //    outputFilename = args[2];
            //}
            return points;
        }

        private void SendCommandsToCodeConnection(Position target, List<Point> points, Rotate rotation,
            Position clip = null)
        {
            // var service = new MinecraftCodeConnectionCommandService();
            var sw = new Stopwatch();


            _minecraftCommandService.Status("preparing schematic");

            if (clip != null)
            {
                points =
                    points.Where(a => a.X >= clip.X && a.Y >= clip.Y && a.Z >= clip.Z)
                        .Select(a => a.Shift(clip.Muliply(-1)))
                        .ToList();
            }
            if (rotation != Rotate.None)
            {
                sw.Start();
                Console.WriteLine($"rotating points...");
                var rotatedPoints = points.AsParallel().Select(a => a.Rotate(rotation)).ToList();
                Console.WriteLine($"time to rotate {sw.Elapsed}");
                sw.Reset();
                var measures = ModelAnalyzer.Analyze(rotatedPoints);
                sw.Start();
                Console.WriteLine($"shifting points...");
                points = rotatedPoints.AsParallel().Select(a => a.Shift(measures.Minimum.Muliply(-1))).ToList();
                Console.WriteLine($"time to shift {sw.Elapsed}");
                sw.Reset();
            }
            sw.Start();
            Console.WriteLine($"combining points...");
            var exportLines = ConvertFileToCommands(points.Where(a => a.BlockId != 0).ToList());
            Console.WriteLine($"time to combine {sw.Elapsed}");
            sw.Reset();
            sw.Start();
            var shift = exportLines.AsParallel().Select(a => a.Shift(target)).ToList();
            Console.WriteLine($"time to shift {sw.Elapsed}");
            sw.Reset();
            sw.Start();
            var importLines =
                shift.AsParallel()
                    .OrderBy(a => a.Start.SortOrder)
                    .ThenBy(a => a.Start.Y)
                    .ThenBy(a => a.Start.X)
                    .ThenBy(a => a.Start.Z)
                    .ToList();
            Console.WriteLine($"time to sort {sw.Elapsed}");

            sw.Reset();

            _minecraftCommandService.Status("starting schematic import");

            sw.Start();
            foreach (var line in importLines)
            {
                var command = _minecraftCommandService.GetFormater()
                    .Fill(line.Start.X, line.Start.Y, line.Start.Z, line.End.X, line.End.Y, line.End.Z, line.BlockName,
                        line.Data.ToString());
                _minecraftCommandService.Command(command);
                    //$"fill?from={line.Start.X} {line.Start.Y} {line.Start.Z}&to={line.End.X} {line.End.Y} {line.End.Z}&tileName={line.BlockName}&tileData={line.Data}");
            }
            sw.Stop();
            _minecraftCommandService.Status($"time to queue commands {sw.Elapsed.TotalSeconds}");
            Console.WriteLine($"time to queue commands {sw.Elapsed.TotalSeconds}");
            sw.Reset();
            sw.Start();
            _minecraftCommandService.Wait();
            sw.Stop();
            _minecraftCommandService.Status($"time to complete import {sw.Elapsed.TotalSeconds}");
            Console.WriteLine($"time to complete import {sw.Elapsed.TotalSeconds}");
        }

        private static List<Line> ConvertFileToCommands(List<Point> points)
        {
            return LineFactory.CreateFromPoints(points);
        }
    }
}