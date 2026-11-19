namespace DialogueEngine;

/// <summary>Who can speak. The game layer answers from its catalog characters; the engine never invents a speaker.</summary>
public interface ISpeakerCatalog
{
    bool HasSpeaker(string id);
}