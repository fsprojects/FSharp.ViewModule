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
open System.Text.RegularExpressions

type ValidationStep<'a> =
| Valid of name : string * value : 'a
| Invalid of name : string * error : string

[<AutoOpen>]
module Validators =
    let createValidator predicate (error : string) (step : ValidationStep<'a>) =
        match step with
        | Invalid(name, err) -> Invalid(name, err)
        | Valid(name, x) ->
            if predicate x then Valid(name, x)
            else Invalid(name, String.Format(error, name, x))

    let validate name value = Valid(name, value)
    
    // String validations
    let notNullOrWhitespace (str : ValidationStep<string>) = 
        let validation value = not(String.IsNullOrWhiteSpace(value))
        createValidator validation "{0} cannot be null or empty" str 

    let noSpaces (str : ValidationStep<string>) = 
        let validation (value : string) = not(value.Contains(" "))
        createValidator validation "{0} cannot contain a space" str

    let hasLength (length : int) (str : ValidationStep<string>) = 
        let validation (value : string) = value.Length = length
        createValidator validation ("{0} must be " + length.ToString() + " characters long") str

    let hasLengthAtLeast (length : int) (str : ValidationStep<string>) = 
        let validation (value : string) = value.Length >= length
        createValidator validation ("{0} must be at least " + length.ToString() + " characters long") str

    let hasLengthNoLongerThan (length : int) (str : ValidationStep<string>) = 
        let validation (value : string) = value.Length <= length
        createValidator validation ("{0} must be no longer than " + length.ToString() + " characters long") str
        
    let private matchesPatternInternal (pattern : string) (errorMsg : string) (str : ValidationStep<string>) =
        let validation (value : string) = Regex.IsMatch(value, pattern)
        createValidator validation errorMsg str

    let matchesPattern (pattern : string) =
        matchesPatternInternal pattern ("{0} must match following pattern: " + pattern)

    let isAlphanumeric =
        matchesPatternInternal "[^a-zA-Z0-9]" "{0} must be alphanumeric"

    let containsAtLeastOneDigit = 
        matchesPatternInternal "[0-9]" "{0} must contain at least one digit"

    let containsAtLeastOneUpperCaseCharacter =
        matchesPatternInternal "[A-Z]" "{0} must contain at least one uppercase character"

    let containsAtLeastOneLowerCaseCharacter =
        matchesPatternInternal "[a-z]" "{0} must contain at least one lowercase character"

    // Generic validations
    let notEqual value step = 
        createValidator (fun v -> value <> v) "{0} cannot equal {1}" step

    let greaterThan value step =
        createValidator (fun v -> v > value) "{0} must be greater than {1}" step

    let greaterOrEqualTo value step =
        createValidator (fun v -> v >= value) "{0} must be greater than or equal to {1}" step

    let lessThan value step =
        createValidator (fun v -> v < value) "{0} must be less than {1}" step

    let lessOrEqualTo value step =
        createValidator (fun v -> v < value) "{0} must be less than or equal to {1}" step
    
    let result (step : ValidationStep<'a>) : Option<string> =
        match step with
        | Valid(_, value) -> None
        | Invalid(_, err) -> Some err

    /// Produces a result of the validation, using a custom error message if an error occurred
    let resultWithError customErrorMessage (step : ValidationStep<'a>) : Option<string> =
        match step with
        | Valid(_, value) -> None
        | Invalid(_, err) -> Some customErrorMessage

