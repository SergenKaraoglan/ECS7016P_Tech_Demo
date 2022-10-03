using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using UnityEditor;

public class WFC_Generator : MonoBehaviour
{

    // button to generate levels
    public Button genButton;

    // Contains tile constraints / rules for each unique tile
    private Dictionary<TileBase, HashSet<TileBase>[]> tileConstraints = new Dictionary<TileBase, HashSet<TileBase>[]>();
    // Global tile count for each unique tile. Used to calculate entropy when selecting a tile.
    private Dictionary<TileBase, int> tileCount = new Dictionary<TileBase, int>();
    // Random generator for breaking ties when selecting a tile
    private System.Random random = new System.Random();
    // Used to simulate cardinal directions (East, South, West, North)
    private int[][] directions = new int[][] {new int[] {1,0}, new int[] {0,1}, new int[] {-1,0}, new int[] {0,-1}};

    // The tilemap rendered in the scene
    public Tilemap tilemap;

    // Height and width of output tilemap
    [Header("Height")]
    [Range(1, 20)]
    public int height;
    [Header("Width")]
    [Range(1, 20)]
    public int width;

    // Output tilemap that will be rendered
    public HashSet<TileBase>[][] outputTilemap;
    // contains the entropy for each position on the output tilemap
    private double[][] entropyArray;

    void Start(){
        // add onclick listener to button
        genButton.onClick.AddListener(Generate);

        // initialise outputTilemap and entropy arrays
        outputTilemap = new HashSet<TileBase>[height][];
        entropyArray = new double[height][];
        for (int i = 0; i < height; i++){
            outputTilemap[i] = new HashSet<TileBase>[width];
            entropyArray[i] = new double[width];
            for (int j = 0; j < width; j++){
                outputTilemap[i][j] = new HashSet<TileBase>();
            }
        }

        // Generate constraints for WFC
        GenerateConstraints();
        // intialise output tilemap
        IntialiseOutputTilemap();
        // intialise entropy values for all tile locations
        IntialiseEntropy();
        // Run loop for WFC
        WFCloop();
    }

    public void GenerateConstraints(){
        // coordinates for input tiles
        int x = -3;
        int y = -9;

        int x2 = 8;
        int y2 = 2;

        // Constraints are generated for each unique tile and its cardinal positions based on the input tilemap
        for (int i = x; i <= x2; i++){
            for (int j = y; j <= y2; j++){
                TileBase tile = tilemap.GetTile(new Vector3Int(i,j,0));

                // Generate key value pair for each unique tile
                if (tileConstraints.ContainsKey(tile)){
                    // get global count for each tile
                    tileCount[tile] += 1;
                }
                else{
                    // 4 HashSets are created for each cardinal position
                    tileConstraints.Add(tile, new HashSet<TileBase>[]{new HashSet<TileBase>(), new HashSet<TileBase>(), 
                    new HashSet<TileBase>(), new HashSet<TileBase>()});
                    tileCount.Add(tile, 1);
                }

                // Check surrounding tiles and add them as a constraint for the current tile
                for (int k = 0; k < 4; k++){
                    int[] dir = directions[k];
                    if (x <= i+dir[0] && i+dir[0] <= x2 && y <= j+dir[1] && j+dir[1] <= y2){
                        tileConstraints[tile][k].Add(tilemap.GetTile(new Vector3Int(i+dir[0],j+dir[1],0)));
                    }
                }
            }
        }
        
        // Clear tilemap
        tilemap.ClearAllTiles();
    }

    public void IntialiseOutputTilemap(){
        // Add all unique tiles to every coordinate for the output tilemap
        for (int i = 0; i < height; i++){
            for (int j = 0; j < width; j++){
                foreach(KeyValuePair<TileBase, HashSet<TileBase>[]> item in tileConstraints){
                    outputTilemap[i][j].Add(item.Key);
                }
            }
        }

    }

    public void WFCloop(){
        while (true){
            // get tile coordinates
            int[] tileCoor = PickTileCoor();
            /*
            Debug.Log(tileCoor[0].ToString() + " " + tileCoor[1].ToString());
            Debug.Log(outputTilemap[tileCoor[1]][tileCoor[0]].Count);
            Debug.Log(CalculateEntropy(outputTilemap[tileCoor[1]][tileCoor[0]]));
            */
            if (tileCoor == null){
                break;
            }
            
            // pick a tile for selected coordinate
            TileBase tile = PickTile(tileCoor[1], tileCoor[0]);
            // render tile chosen
            RenderTile(tileCoor[1], tileCoor[0], tile);

            // propagate new constraints based on tile selected
            Propagate(tileCoor[1], tileCoor[0], tile);

        }
    }

    public int[] PickTileCoor(){
        double minEntropy = Double.MaxValue;
        ArrayList tiles = new ArrayList();
        // pick tile based on minimal entropy
        for (int y = 0; y < height; y++){
            for (int x = 0; x < width; x++){
                double entropy = entropyArray[y][x];
                if (entropy != 0 && entropy < minEntropy){
                    minEntropy = entropy;
                    tiles.Clear();
                    tiles.Add(new int[] {y, x});
                }
                else if (entropy == minEntropy){
                    tiles.Add(new int[] {y, x});
                }
            }
        }

        int count = tiles.Count;
        if (count == 0){
            return null;
        }
        if (count > 1){
            // break ties randomly
            return (int[]) tiles[random.Next(0, count)];
        }
        return (int[]) tiles[0];
    }

    public TileBase PickTile(int x, int y){
        HashSet<TileBase> tiles = outputTilemap[y][x];
        // random number between 0 and 1 used to select a tile randomly
        double select = random.NextDouble();

        // calculate sum of the global tile frequencies for each tile within a set
        int sum = 0;
        foreach(TileBase tile in tiles){
            sum += tileCount[tile];
        }

        int i = 0;
        double[] probs = new double[tiles.Count];
        // pick a tile based on the probability of each tile which is accumulated and compared with a random number between 0 and 1
        foreach(TileBase tile in tiles){
            probs[i] = ((double) tileCount[tile]/ (double)sum);
            if (i > 0){
                probs[i] += probs[i-1];
            }

            if (select <= probs[i]){
                return tile;
            }
            i++;  
        }
        return null;
    }

    public void Propagate(int x, int y, TileBase initialTile){
        // set entropy to 0
        entropyArray[y][x] = 0.0;
        // update constraints
        outputTilemap[y][x].Clear();
        outputTilemap[y][x].Add(initialTile);

        // contains all tile locations that need to be checked
        Stack tileCoor = new Stack();
        // add adjacent tiles to stack
        for (int i = 0; i < 4; i++){
            int[] dir = directions[i];
            if (0 <= x+dir[0] && x+dir[0] < width && 0 <= y+dir[1] && y+dir[1] < height){
                tileCoor.Push(new int[][] {new int[] {x+dir[0], y+dir[1]}, new int[] {x, y}});
            }
        }

        // store previous checked location
        int[] prev;
        int dirIdx = 0;
        // update restrictions on adjacent tiles based on adjaceny rules
        while (tileCoor.Count > 0){
            int[][] coor = (int[][]) tileCoor.Pop();
            x = coor[0][0];
            y = coor[0][1];
            prev = coor[1];
            
            // get previous cardinal position
            for (int i = 0; i < 4; i++){
                int[] dir = directions[i];
                if (prev[0] == (x + dir[0]) && prev[1] == (y + dir[1])){
                    dirIdx = i;
                }
            }

            // check if current potential tiles are valid according to the constraints
            ArrayList constraints = new ArrayList();
            foreach (TileBase tile in outputTilemap[y][x]){
                bool contains = false;
                foreach (TileBase tile2 in outputTilemap[prev[1]][prev[0]]){
                    if (tileConstraints[tile][dirIdx].Contains(tile2)){
                        contains = true;
                        break;
                    }
                }
                if (!contains){
                    constraints.Add(tile);
                }
            }
            
            // remove tiles that don't satisfy the constraint rules
            foreach (TileBase tile in constraints){
                outputTilemap[y][x].Remove(tile);
            }

            // compare with previous changes
            // add adjacent tiles to stack if constraints were changed
            if (constraints.Count > 0){
                for(int i = 0; i < 4; i++){
                    int[] dir = directions[i];
                    if (0 <= x+dir[0] && x+dir[0] < width && 0 <= y+dir[1] && y+dir[1] < height){
                        tileCoor.Push(new int[][] {new int[] {x+dir[0], y+dir[1]}, new int[] {x, y}});
                    }
                }
            }

            // update entropy for any tile constraints changed
            entropyArray[y][x] = CalculateEntropy(outputTilemap[y][x]);
            
            // render tile if entropy is 0
            if (entropyArray[y][x] == 0){
                TileBase tileInput = null;
                foreach (TileBase tile in outputTilemap[y][x]){
                    tileInput = tile;
                }
                RenderTile(x, y, tileInput);
            }
            
        }

        
    }

    public double CalculateEntropy(HashSet<TileBase> tiles){
        // calculate sum of global tile counts
        int sum = 0;
        foreach(TileBase tile in tiles){
            sum += tileCount[tile];
        }

        // calculate entropy
        double entropy = 0;
        foreach(TileBase tile in tiles){
            double p = ((double) tileCount[tile]/ (double)sum);
            entropy -= p * Math.Log(p, 2);
        }
        
        return entropy;
    }

    public void IntialiseEntropy(){
        // calculate entropy for first tile location and use the value to fill entire entropy array as it is the same at the start
        double entropy = CalculateEntropy(outputTilemap[0][0]);
        for (int i = 0; i < height; i++){
            for (int j = 0; j < width; j++){
                entropyArray[i][j] = entropy;
            }
        }
    }

    public void RenderTile(int x, int y, TileBase tile){
        tilemap.SetTile(new Vector3Int(x, y, 0), tile);
    }

    public void Generate(){
        // clear tilemap
        tilemap.ClearAllTiles();

        // reset output tiles
        IntialiseOutputTilemap();
        // reset entropy
        IntialiseEntropy();
        // run WFC loop
        WFCloop();

    }

}