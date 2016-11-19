module ApiCredentials

open FSharp.Data
type ApiCredentials = JsonProvider<"apiCred.json">

let credentials = ApiCredentials.Load("apiCred.json")
    
  


