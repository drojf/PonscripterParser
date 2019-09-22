################## BEGIN PRELUDE #################

init python:
	# Used for stralias and numalias
    # Used for stralias and numalias
    class VariableArray:
        def __init__(self, length, default_value):
            self.default_value = default_value
            self.values = [self.default_value] * length
            self.length = length

        def __getitem__(self, key):
            if key >= self.length:
                renpy.log("WARNING: attempt to get {} when size is {}".format(key, self.length))
                return self.default_value
            else:
                return self.values[key]

        def __setitem__(self, key, value):
            if key >= self.length:
                renpy.log("WARNING: attempt to set {} when size is {}".format(key, self.length))
            else:
                self.values[key] = value

    # Setup the (numeric) variable array (need to double check the actual variable count limit)
    pons_var = VariableArray(10000, 0)
    pons_str = VariableArray(10000, "")

	#def makeArrayRec(sizes, depth):
	#	if depth < len(sizes):
	#		return [makeArrayRec(sizes, depth+1) for _ in range(sizes[depth])]
	#	else:
	#		return 0
	#
	#def makeArray(*sizes):
	#	return makeArrayRec(sizes, 0)

	class MultiDimPhantom:
		def __init__(self, root_object, accessed_values):
			self.root_object = root_object
			self.accessed_values = accessed_values

		def __getitem__(self, key):
			self.accessed_values.append(key)

			if len(self.accessed_values) >= len(self.root_object.dimensions):
				return self.root_object.get(self.accessed_values)
			else:
				return MultiDimPhantom(self.root_object, self.accessed_values)

		def __setitem__(self, key, value):
			self.accessed_values.append(key)

			if len(self.accessed_values) == len(self.root_object.dimensions):
				return self.root_object.set(self.accessed_values, value)
			else:
				raise Exception("too few arguments for array set()")

	# Used for ponscripter arrays
	class Dim:
		def __init__(self, dimensions):
			self.dimensions = dimensions
			accumulator = 1
			for dimension in dimensions:
				accumulator *= dimension
			self.arr = [0] * accumulator

		def get(self, indices):
			return self.arr[self.calculate_index(indices)]

		def set(self, indices, value):
			self.arr[self.calculate_index(indices)] = value

		def calculate_index(self, indices):
			self.dimension_check(indices)
			multiplier = 1
			index = 0
			for i in range(len(self.dimensions)):
				index += indices[i] * multiplier
				multiplier *= self.dimensions[i]

			return index

		def dimension_check(self, indices):
			if len(indices) != len(self.dimensions):
				raise Exception("not enough indices - got {} expected {}".format(indices, self.dimensions))

			# check each indice is within the corresponding dimension limit
			for i in range(len(self.dimensions)):
				if not (indices[i] < self.dimensions[i]):
					raise Exception("Index [{}] (arg {}) was out of range - ind: {} dim: {}".format(indices[i], i, indices, self.dimensions))

		def __getitem__(self, firstKey):
			if len(self.dimensions) == 1:
				return self.get([firstKey])
			else:
				return MultiDimPhantom(self, [firstKey])

		# the only time this happens is if the value is directly set like a[3] = 5
		def __setitem__(self, singularKey, value):
			self.set([singularKey], value)


# Declare characters used by this game.
define narrator = Character(_("Narrator"), color="#c8ffc8")

label start:
################### END PRELUDE ##################