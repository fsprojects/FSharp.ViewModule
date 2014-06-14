(*
Copyright (c) 2013-2014 FSharp.ViewModule Team

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*)

namespace FSharp.ViewModule.Core.Validation

open System

type ValidationResult<'a> =
| Valid of name : string * value : 'a
| Invalid of name : string * value : 'a * error : string list

[<AutoOpen>]
module Validators =
    let createValidator predicate (error : string) (step : ValidationResult<'a>) =        
        let success = 
            match step with
            | Invalid(_, value, _) -> predicate value
            | Valid(_, value) -> predicate value
        
        match success, step with
        | true, Valid(name, value) -> Valid(name, value)
        | true,  Invalid(name, value, err) -> Invalid(name, value, err)
        | false, Valid(name, value) -> Invalid(name, value, [String.Format(error, name, value)])
        | false, Invalid(name, value, err) -> Invalid(name, value, err @ [String.Format(error, name, value)])

    let validate name value = Valid(name, value)
    
    // String validations
    let notNullOrWhitespace (str : ValidationResult<string>) = 
        let validation value = not(String.IsNullOrWhiteSpace(value))
        createValidator validation "{0} cannot be null or empty." str 

    let noSpaces (str : ValidationResult<string>) = 
        let validation (value : string) = not(value.Contains(" "))
        createValidator validation "{0} cannot contain a space." str

    let hasLength (length : int) (str : ValidationResult<string>) = 
        let validation (value : string) = value.Length = length
        createValidator validation ("{0} must be " + length.ToString() + " characters long.") str

    let hasLengthAtLeast (length : int) (str : ValidationResult<string>) = 
        let validation (value : string) = value.Length >= length
        createValidator validation ("{0} must be at least " + length.ToString() + " characters long.") str

    let hasLengthNoLongerThan (length : int) (str : ValidationResult<string>) = 
        let validation (value : string) = value.Length <= length
        createValidator validation ("{0} must be no longer than " + length.ToString() + " characters long.") str

    // Generic validations
    let notEqual value step = 
        createValidator (fun v -> value <> v) "{0} cannot equal {1}." step

    let greater value step =
        createValidator (fun v -> v > value) "{0} must be greater than {1}." step

    let greaterOrEqual value step =
        createValidator (fun v -> v >= value) "{0} must be greater than or equal to {1}." step

    let lessOrEqual value step =
        createValidator (fun v -> v < value) "{0} must be less than or equal to {1}." step
    
    let result (step : ValidationResult<'a>) : Option<string list> =
        match step with
        | Valid(_, _) -> None
        | Invalid(_, _, err) -> Some err

    /// Produces a result of the validation, using a custom error message if an error occurred
    let resultWithError customErrorMessage (step : ValidationResult<'a>) : Option<string list> =
        match step with
        | Valid(_, value) -> None
        | Invalid(_, _, _) -> Some [customErrorMessage]

