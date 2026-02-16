using LiteDB;
using Overseer.Server.Integration.Machines;
using Overseer.Server.Plugins;

namespace Overseer.Server.Mappers;

public static class MachineMapper
{
  public static void Map()
  {
    BsonMapper.Global.RegisterType(
      serialize: machine =>
      {
        var type = machine.GetType();
        var doc = new BsonDocument
        {
          ["_type"] = $"{type.FullName}, {type.Assembly.GetName().Name}",
          ["Name"] = machine.Name,
          ["Disabled"] = machine.Disabled,
          ["WebcamUrl"] = machine.WebcamUrl,
          ["WebcamOrientation"] = machine.WebcamOrientation?.ToString(),
          ["Tools"] = new BsonArray(machine.Tools.Select(tool => BsonMapper.Global.Serialize(tool))),
          ["SortIndex"] = machine.SortIndex,
          ["MachineType"] = machine.MachineType,
          ["Properties"] = new BsonDocument(machine.Properties.ToDictionary(kvp => kvp.Key, kvp => BsonMapper.Global.Serialize(kvp.Value))),
        };

        if (machine.Id != 0)
        {
          doc["_id"] = machine.Id;
        }

        return doc;
      },
      deserialize: doc =>
      {
        var typeName = doc["_type"].AsString;
        var type = PluginDiscoveryService.FindTypes(t => $"{t.FullName}, {t.Assembly.GetName().Name}" == typeName).FirstOrDefault();
        Machine machine;
        if (type != null)
        {
          machine = (Machine)Activator.CreateInstance(type)!;
        }
        else
        {
          machine = new Machine();
        }

        machine.Id = doc["_id"].AsInt32;
        machine.Name = doc["Name"].AsString;
        machine.Disabled = doc["Disabled"].AsBoolean;
        machine.WebcamUrl = doc["WebcamUrl"].AsString;
        machine.WebcamOrientation = Enum.TryParse<MachineWebcamOrientation>(doc["WebcamOrientation"].AsString, out var orientation)
          ? orientation
          : null;
        machine.Tools = [.. doc["Tools"].AsArray.Select(toolDoc => BsonMapper.Global.Deserialize<MachineTool>(toolDoc))];
        machine.SortIndex = doc["SortIndex"].AsInt32;
        machine.MachineType = doc["MachineType"].AsString;
        machine.Properties = doc["Properties"].AsDocument.ToDictionary(kvp => kvp.Key, kvp => BsonMapper.Global.Deserialize<object>(kvp.Value));
        return machine;
      }
    );
  }
}
