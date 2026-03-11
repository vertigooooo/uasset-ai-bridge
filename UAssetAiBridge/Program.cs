// Program.cs
using System;
using System.IO;
using System.Text.Json;

using UAssetAPI;
using UAssetAPI.UnrealTypes;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            // 错误必须是结构化 JSON（遵守 AI contract）
            Console.WriteLine(JsonSerializer.Serialize(new {
                error = "no_path_provided",
                message = "Usage: uasset-ai-bridge inspect <file.uasset> [engineVersion]"
            }));
            return 2;
        }

        string path = args[0];
        if (!File.Exists(path))
        {
            Console.WriteLine(JsonSerializer.Serialize(new {
                error = "file_not_found",
                path = path
            }));
            return 3;
        }

        try
        {
            // **示例**：文档中最简单的构造函数需要 path + EngineVersion （你也可以接受参数或尝试自动检测）
            // 这里演示用 VER_UE5_1 作为占位（实际使用时可从 args 读或实现自动检测）
            EngineVersion ev = EngineVersion.VER_UE5_1;
            UAsset myAsset = new UAsset(path, ev);

            var output = new {
                file = new {
                    path = Path.GetFullPath(path),
                    size_bytes = new FileInfo(path).Length
                },
                summary = new {
                    export_count = myAsset.Exports.Count
                }
            };

            Console.WriteLine(JsonSerializer.Serialize(output));
            return 0;
        }
        catch (Exception ex)
        {
            // 所有异常也输出为结构化 JSON，便于 Agent 解析
            Console.WriteLine(JsonSerializer.Serialize(new {
                error = "parse_failed",
                message = ex.Message,
                exception_type = ex.GetType().FullName
            }));
            return 4;
        }
    }
}