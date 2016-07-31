(*
Copyright (c) 2013-2015 FSharp.ViewModule Team

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

namespace FSharp.ViewModule.Validation

open System
open System.Text.RegularExpressions

type ValidationResult<'a> =
| Valid of name : string * value : 'a
| InvalidCollecting of name : string * value : 'a * error : string list
| InvalidDone of name : string * value : 'a * error : string list

[<AutoOpen>]
module Validators =
    /// Create a custom validator using a predicate ('a -> bool) and an error message on failure. The error message can use {0} for a placeholder for the property name.
    let custom (validator : 'a -> string option) (step : ValidationResult<'a>) =        
        let success = 
            match step with            
            | InvalidDone(_, _, _) -> None // Short circuit
            | InvalidCollecting(_, value, _) -> validator value
            | Valid(_, value) -> validator value
        
        match success, step with
        | _, InvalidDone(_, _, _) -> step // If our errors are fixed coming in, just pass through
        | None, Valid(name, value) -> Valid(name, value)
        | None,  InvalidCollecting(name, value, err) -> InvalidCollecting(name, value, err)
        | Some error, Valid(name, value) -> InvalidCollecting(name, value, [String.Format(error, name, value)])
        | Some error, InvalidCollecting(name, value, err) -> InvalidCollecting(name, value, err @ [String.Format(error, name, value)])

    /// Fix the current state of errors, bypassing all future validation checks if we're in an error state
    let fixErrors (step : ValidationResult<'a>) =
        match step with
        | InvalidCollecting(name, value, errors) -> 
            // Switch us from invalid to invalid with errors fixed
            InvalidDone(name, value, errors)
        | _ -> step

    /// Fix the current state of errors, bypassing all future validation checks if we're in an error state
    /// Also supplies a custom error message to replace the existing
    let fixErrorsWithMessage errorMessage (step : ValidationResult<'a>) =
        match step with
        | InvalidCollecting(name, value, errors) -> 
            // Switch us from invalid to invalid with errors fixed
            InvalidDone(name, value, [errorMessage])
        | _ -> step

    /// Begin a validation chain for a given property name
    let validate propertyName value = Valid(propertyName, value)
    
    // String validations
    let notNullOrWhitespace (str : ValidationResult<string>) = 
        let validation value = if String.IsNullOrWhiteSpace(value) then Some "{0} cannot be null or empty." else None            
        custom validation  str 

    let noSpaces (str : ValidationResult<string>) = 
        let validation (value : string) = if value.Contains(" ") then Some "{0} cannot contain a space." else None
        custom validation str

    let hasLength (length : int) (str : ValidationResult<string>) = 
        let validation (value : string) = if value.Length <> length then Some ("{0} must be " + length.ToString() + " characters long.") else None
        custom validation str

    let hasLengthAtLeast (length : int) (str : ValidationResult<string>) = 
        let validation (value : string) = if value.Length < length then Some ("{0} must be at least " + length.ToString() + " characters long.") else None
        custom validation str

    let hasLengthNoLongerThan (length : int) (str : ValidationResult<string>) = 
        let validation (value : string) = if value.Length > length then Some ("{0} must be no longer than " + length.ToString() + " characters long") else None
        custom validation str
        
    let private matchesPatternInternal (pattern : string) (errorMsg : string) (str : ValidationResult<string>) =
        let validation (value : string) = if Regex.IsMatch(value, pattern) then None else Some errorMsg
        custom validation str

    let matchesPattern (pattern : string) str =
        matchesPatternInternal pattern ("{0} must match following pattern: " + pattern) str

    let isAlphanumeric str =
        matchesPatternInternal "[^a-zA-Z0-9]" "{0} must be alphanumeric" str

    let containsAtLeastOneDigit str = 
        matchesPatternInternal "[0-9]" "{0} must contain at least one digit" str

    let containsAtLeastOneUpperCaseCharacter str =
        matchesPatternInternal "[A-Z]" "{0} must contain at least one uppercase character" str

    let containsAtLeastOneLowerCaseCharacter str =
        matchesPatternInternal "[a-z]" "{0} must contain at least one lowercase character" str

    // Generic validations
    let notEqual value step = 
        let validation v = if value = v then Some ("{0} cannot equal " + value.ToString()) else None
        custom validation step

    let greaterThan value step =
        let validation v = if v > value then None else Some ("{0} must be greater than " + value.ToString())
        custom validation step

    let greaterOrEqualTo value step =
        let validation v = if v >= value then None else Some ("{0} must be greater than or equal to " + value.ToString())
        custom validation step

    let lessThan value step =
        let validation v = if v < value then None else Some ("{0} must be less than " + value.ToString())
        custom validation step

    let lessOrEqualTo value step =
        let validation v = if v <= value then None else Some ("{0} must be less than or equal to " + value.ToString())
        custom validation step

    let isBetween lowerBound upperBound step =
        let validation v = if lowerBound <= v && v <= upperBound then None else Some ("{0} must be between " + lowerBound.ToString() + " and " + upperBound.ToString())
        custom validation step
    
    let containedWithin collection step =
        let validation value = if Option.isSome (Seq.tryFind ((=) value) collection) then None else Some ("{0} must be one of: " + String.Join(", ", Seq.map (fun i -> i.ToString()) collection))
        custom validation step

    let notContainedWithin collection step =
        let validation value = if Option.isNone (Seq.tryFind ((=) value) collection) then None else Some ("{0} cannot be one of: " + String.Join(", ", Seq.map (fun i -> i.ToString()) collection))
        custom validation step

    let result (step : ValidationResult<'a>) : string list =
        match step with
        | Valid(_, _) -> []
        | InvalidCollecting(_, _, err) -> err
        | InvalidDone(_, _, err) -> err

    /// Produces a result of the validation, using a custom error message if an error occurred
    let resultWithError customErrorMessage (step : ValidationResult<'a>) : string list =
        match step with
        | Valid(_, value) -> []
        | _ -> [customErrorMessage]

    let evaluate value (workflow : 'a -> string list) =
        workflow(value)


namespace CSharp.ViewModule.Validation

open System

open FSharp.ViewModule.Validation

type Validator<'a> = internal Validator of Func<ValidationResult<'a>, ValidationResult<'a>> with
    static member internal ofFunc v = Func<ValidationResult<'a>, ValidationResult<'a>>(v) |> Validator

    member validator.FixErrors() =
        match validator with Validator v -> (fun r -> v.Invoke r |> Validators.fixErrors) |> Validator.ofFunc

    member validator.FixErrors (message) =
        match validator with Validator v -> (fun r -> v.Invoke r |> Validators.fixErrorsWithMessage message) |> Validator.ofFunc

    member validator.Concat (Validator other) =
        match validator with Validator v -> v.Invoke >> other.Invoke |> Validator.ofFunc

type StepResult = internal StepResult of string option with 
    static member Pass() = StepResult None
    static member Fail(error : string) = StepResult (Some error)
    static member internal unwrap (StepResult r) = r

type Validators internal () =

    static member CreateCustom (validate : Func<'a, StepResult>) = 
        Validators.custom (validate.Invoke >> StepResult.unwrap) |> Validator.ofFunc

    static member NotNullOrWhitespace() = Validators.notNullOrWhitespace |> Validator.ofFunc

    static member NoSpaces() = Validators.noSpaces |> Validator.ofFunc
    
    static member HasLength(length) = Validators.hasLength length |> Validator.ofFunc

    static member HasLengthAtLeast(length) = Validators.hasLengthAtLeast length |> Validator.ofFunc

    static member HasLengthNotLongerThan(length) = Validators.hasLengthNoLongerThan length |> Validator.ofFunc

    static member MatchesPattern(pattern) = Validators.matchesPattern pattern |> Validator.ofFunc

    static member IsAlphanumeric() = Validators.isAlphanumeric |> Validator.ofFunc
    
    static member ContainsAtLeastOneDigit() = Validators.containsAtLeastOneDigit |> Validator.ofFunc

    static member ContainsAtLeastOneUpperCaseCharacter = Validators.containsAtLeastOneUpperCaseCharacter |> Validator.ofFunc

    static member ContainsAtLeastOneLowerCaseCharacter = Validators.containsAtLeastOneLowerCaseCharacter |> Validator.ofFunc

    static member NotEqual(value) = Validators.notEqual value |> Validator.ofFunc

    static member GreaterThan(value) = Validators.greaterThan value |> Validator.ofFunc

    static member GreaterOrEqualTo(value) = Validators.greaterOrEqualTo value |> Validator.ofFunc

    static member LessThan(value) = Validators.lessThan value |> Validator.ofFunc

    static member LessOrEqualTo(value) = Validators.lessOrEqualTo value |> Validator.ofFunc

    static member IsBetween(lowerBound, upperBound) = Validators.isBetween lowerBound upperBound |> Validator.ofFunc

    static member ContainedWithin(collection) = Validators.containedWithin collection |> Validator.ofFunc

    static member NotContainedWithin(collection) = Validators.notContainedWithin collection |> Validator.ofFunc