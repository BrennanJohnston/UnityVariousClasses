using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapVote {
    Dictionary<ulong, int> voteDictionary = new Dictionary<ulong, int>();
    string[] mapNames;

    public static MapVote Singleton { get; private set; } = new MapVote();

    private MapVote() {
        mapNames = new string[0];
    }

    private MapVote(string[] mapNamesArray) {
        mapNames = mapNamesArray;
    }

    /// <summary>
    /// Initializes the Singleton and begins a vote.
    /// </summary>
    /// <param name="mapNamesArray"></param>
    public void StartVote(string[] mapNamesArray) {
        Singleton = new MapVote(mapNamesArray);
    }

    public void CastVote(ulong clientId, int voteIndex) {
        if (voteIndex < 0 || voteIndex > mapNames.Length - 1) return;

        voteDictionary[clientId] = voteIndex;
    }

    /// <summary>
    /// Get the winner of the vote as it currently stands.
    /// </summary>
    /// <returns>The map name (string) of the winner of the vote, null if constructor mapNamesArray had length 0.</returns>
    public string GetWinningMap() {
        int[] voteCounts = TallyVotes();

        if(voteCounts.Length < 1) return null;

        int highestCountIndex = 0;
        for(int i = 1; i < voteCounts.Length; i++) {
            if (voteCounts[i] > highestCountIndex) highestCountIndex = i;
        }

        return mapNames[highestCountIndex];
    }

    /// <summary>
    /// Get the index of the winner of the vote as it currently stands.
    /// </summary>
    /// <returns>Index of the winner.  Returns -1 if failed.</returns>
    public int GetWinningMapIndex() {
        int[] voteCounts = TallyVotes();

        if (voteCounts.Length < 1) return -1;

        int highestCountIndex = 0;
        for (int i = 1; i < voteCounts.Length; i++) {
            if (voteCounts[i] > highestCountIndex) highestCountIndex = i;
        }

        return highestCountIndex;
    }

    public int[] GetVoteCounts() {
        return TallyVotes();
    }

    public string[] GetVoteMapNames() {
        return mapNames;
    }

    private int[] TallyVotes() {
        int[] voteCounts = new int[mapNames.Length];

        // I don't think 0'ing is necessary but I'm doing it anyway
        for(int i = 0; i < voteCounts.Length; i++) {
            voteCounts[i] = 0;
        }

        foreach(ulong key in voteDictionary.Keys) {
            int voteIndex = voteDictionary[key];
            voteCounts[voteIndex]++;
        }

        return voteCounts;
    }
}