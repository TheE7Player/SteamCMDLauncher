module main

import os
import crypto.sha256
import time

const (
	target = "json"
	__debug = false
	output = "res_hash.txt"
)

// Written By: TheE7Player (https://github.com/TheE7Player)
// Written for Project: SteamCMDLauncher
// Goal: To get SHA-256 checksums of json files for the file verifier

// Helping Comment -> https://stackoverflow.com/a/51966515

// Returns the filename without the extension -> returns string
fn get_abs_name(file string) string
{
	mut output_f := os.file_name(file)
	trim_len := output_f.len - (target.len + 1)

	return output_f.substr(0, trim_len)
}

fn main() 
{
	println("[!] RUNNING sha256_writer.v [!] ")

	println('Getting list of items')

	// Set the folder location to look for the files. If debug mode is set, it ignores the launch location as this would be wrong.
	folder_location := if __debug { "C:\\Users\\james\\source\\repos\\SteamCMDLauncher\\SteamCMDLauncher\\Resources" } else { os.join_path(os.getwd(), "Resources" ) }

	println('Looking at location: $folder_location')

	game_files := os.walk_ext(folder_location, target)

	if game_files.len < 1 {
		eprintln("Couldn't find any .json where the program was runned under - exiting!")
		return
	}

	// Print out the results of how many were found of the target file extension
	println('Found the following files with "*.$target": $game_files.len')

	write_file_path := os.join_path(folder_location, output)

	println("Writing to file: $write_file_path")

	mut out_file := os.create(write_file_path) or {
		eprintln(err)
		return
	}

	// Get the date when the program ran
	date_run := time.now()

	// Write the initial comments first
	out_file.write_string("# Holds hash keys to validate if the json loaded is official\n# Validation was runned after build at (YYYY-MM-DD): $date_run\n# This process is automated using vlang ~ https://github.com/vlang/v") or {
		eprintln(err)
		return
	}

	// Now we get their hashes
	for file in game_files {

		out_file.write_string("\n") or { eprintln('Failed to insert newline break in file!') return }

		f_name := get_abs_name(file)
	
		bytes := os.read_bytes(file) or { eprintln('Failed to get bytes for this file!') return }

		v_hash := sha256.sum(bytes).hex()

		out_file.write_string("$f_name=$v_hash") or {
			eprintln(err)
			return
		}
	}

	println("Process is complete, it should be located in the folder of ./$write_file_path")
	
	println("[!] ENDED sha256_writer.v [!] ")
}